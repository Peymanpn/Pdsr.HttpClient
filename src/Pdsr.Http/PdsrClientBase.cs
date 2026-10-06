using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Pdsr.Http;

/// <summary>
/// Abstraction of <see cref="IPdsrClientBase"/> with default implementations.
/// </summary>
public abstract class PdsrClientBase : IPdsrClientBase
{
    private readonly HttpClient _client;
    private readonly ILogger<PdsrClientBase> _logger;

    /// <summary>
    /// Maximum number of retries for a single request, used while <see cref="IsRetryRequired"/> returns true.
    /// </summary>
    protected int _retryCount = 5;

    // Number of public entry points (GetString, GetStream, SendAsync<T>) currently running.
    // The outermost one clears the per-request state when it completes, successfully or not.
    private int _requestDepth;

    // NamingStrategy before the per-request configuration ran; restored by ClearConfigs.
    private SerializationNamingStrategy? _namingStrategyBeforeRequest;

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="client">HttpClient. It is not disposed by this instance.</param>
    /// <param name="loggerFactory">Instance of Logger factory</param>
    public PdsrClientBase(HttpClient client, ILoggerFactory loggerFactory)
    {
        _client = client;
        _logger = loggerFactory.CreateLogger<PdsrClientBase>();
        QueryParameters ??= new Dictionary<string, string?>();
    }

    #region Properties
    public Action<HttpRequestMessage>? ConfigRequestMessage { get; set; }
    public Action<IPdsrClientBase>? ConfigHttpClient { get; set; }
    public IDictionary<string, string?> QueryParameters { get; set; }
    public string RequestUrlPath { get; set; } = "";
    public Func<HttpResponseMessage, CancellationToken, Task>? HandleStatusCodeBase { get; set; }
    public Func<HttpResponseMessage?, Exception, CancellationToken, Task>? HandleExceptionAsync { get; set; }

    /// <summary>
    /// If true, it throws an exception on any status lower than 200 and greater than 299.
    /// can be set by <see cref="Extensions.PdsrClientExtensions.EnsureSuccess{TClient}(TClient)"/>
    /// </summary>
    public bool EnsureSuccess { get; set; }

    /// <summary>
    /// Naming strategy to use while serializing and deserializing.
    /// default value <see cref="SerializationNamingStrategy.Camel"/>
    /// </summary>
    public SerializationNamingStrategy NamingStrategy { get; set; } = SerializationNamingStrategy.Camel;

    #endregion


    /// <summary>
    /// Deserialize client output contents
    /// </summary>
    protected virtual JsonSerializerOptions SerializerOptions => PdsrClientDefaults.GetSerializerOptions(NamingStrategy);

    #region Client Capsulation
    /// <summary>
    /// Sends an HTTP request asynchronously.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) => _client.SendAsync(request);

    /// <summary>
    /// Sends an HTTP request asynchronously.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, containing the HTTP response message.</returns>
    public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) => _client.SendAsync(request, cancellationToken);

    /// <summary>
    /// Sends an HTTP request asynchronously with a specified completion option.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="completionOption">A value that indicates when the operation should complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption) => _client.SendAsync(request, completionOption);

    /// <summary>
    /// Sends an HTTP request asynchronously.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="completionOption">A value that indicates when the operation should complete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken = default) => _client.SendAsync(request, completionOption, cancellationToken);

    /// <summary>
    /// Wrapped HttpClient BaseAddress
    /// </summary>
    protected virtual Uri? BaseAddress { get => _client.BaseAddress; set => _client.BaseAddress = value; }

    #endregion

    /// <inheritdoc/>
    public virtual Task<Stream> GetStream(CancellationToken cancellationToken = default) => RunRequest(async () =>
    {
        HttpResponseMessage response = await SendConfiguredRequest(cancellationToken).ConfigureAwait(false);
        return await ReadAsStreamAsync(response.Content, cancellationToken).ConfigureAwait(false);
    });

    /// <inheritdoc/>
    public virtual Task<string?> GetString(CancellationToken cancellationToken = default) => RunRequest<string?>(async () =>
    {
        using HttpResponseMessage response = await SendConfiguredRequest(cancellationToken).ConfigureAwait(false);
        return await ReadAsStringAsync(response.Content, cancellationToken).ConfigureAwait(false);
    });

    /// <inheritdoc/>
    public virtual Task<T?> SendAsync<T>(CancellationToken cancellationToken = default) => RunRequest<T?>(async () =>
    {
        using HttpResponseMessage response = await SendConfiguredRequest(cancellationToken).ConfigureAwait(false);

        // Error bodies are not a T, and empty bodies (e.g. 204) have nothing to deserialize.
        if (!response.IsSuccessStatusCode || response.Content is null || response.Content.Headers.ContentLength == 0)
        {
            return default;
        }

        using Stream stream = await ReadAsStreamAsync(response.Content, cancellationToken).ConfigureAwait(false);
        try
        {
            return await Deserialize<T>(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex) when (!EnsureSuccess)
        {
            _logger.LogWarning(ex, "Could not deserialize the response of {Url} as {Type}", response.RequestMessage?.RequestUri, typeof(T));
            return default;
        }
    });

    /// <summary>
    /// Logic to Sets the base address.
    /// Called once per request, before it is sent. <see cref="HttpClient.BaseAddress"/> cannot be changed
    /// after the first request, so implementations should only set it when it is null.
    /// </summary>
    protected abstract Task SetBaseAddress(CancellationToken cancellationToken = default);

    /// <summary>
    /// Add the required logic for authorization by implementing this method.
    /// Called before every attempt, including retries.
    /// </summary>
    /// <param name="request">Instance of HttpRequestMessage.
    /// Authorization token should be added to request if each request needs different authorization. otherwise can add to the HttpClient</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected abstract Task SetAuthorizationHeader(HttpRequestMessage request, CancellationToken cancellationToken = default);

    /// <summary>
    /// This method will be called if any custom Logging required.
    /// </summary>
    /// <param name="response">instance of the sent request message</param>
    /// <param name="elapsed">Time taken by the request, in milliseconds</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected virtual Task WriteLog(HttpResponseMessage response, long elapsed, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Main method to sends use the delegates and configures both request and client.
    /// When called outside <see cref="GetString"/>, <see cref="GetStream"/> or <see cref="SendAsync{T}(CancellationToken)"/>,
    /// it clears the per-request configuration when it completes.
    /// </summary>
    /// <returns>The response after sending the request</returns>
    protected virtual async Task<HttpResponseMessage> ConfigAndSend(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        bool ownsRequestState = _requestDepth == 0;
        try
        {
            await SetBaseAddress(cancellationToken).ConfigureAwait(false);

            ApplyRequestConfigs(request);

            byte[]? body = null;
#if NETSTANDARD2_0
            // .NET Framework's HttpClient disposes the request content once sent; keep a copy so retries can resend it.
            if (request.Content is not null && _retryCount > 0)
            {
                body = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
#endif

            HttpResponseMessage response = await SendAndHandle(request, cancellationToken).ConfigureAwait(false);

            int retriesLeft = _retryCount;
            while (retriesLeft > 0 && await IsRetryRequired(response, cancellationToken).ConfigureAwait(false))
            {
                retriesLeft--;

                if (body is null && request.Content is not null)
                {
                    body = await ReadAsByteArrayAsync(request.Content, cancellationToken).ConfigureAwait(false);
                }

                HttpRequestMessage retryRequest = CreateRetryRequest(request, body);

                _logger.LogInformation("Retrying {Method} {Url} after {StatusCode}, retries remaining: {Retries}",
                    retryRequest.Method, retryRequest.RequestUri, (int)response.StatusCode, retriesLeft);

                response.Dispose();
                response = await SendAndHandle(retryRequest, cancellationToken).ConfigureAwait(false);
            }

            if (!response.IsSuccessStatusCode && EnsureSuccess)
            {
                string message = string.Format("Response status code does not indicate success: {0} ({1})", response.StatusCode, response.ReasonPhrase);
                response.Dispose();
                throw new HttpRequestException(message, inner: null);
            }

            return response;
        }
        finally
        {
            if (ownsRequestState)
            {
                ClearConfigs();
            }
        }
    }

    /// <summary>
    /// Applies the per-request configuration delegates and query parameters to <paramref name="request"/>.
    /// </summary>
    private void ApplyRequestConfigs(HttpRequestMessage request)
    {
        _namingStrategyBeforeRequest ??= NamingStrategy;

        ConfigRequestMessage?.Invoke(request);

        ConfigHttpClient?.Invoke(this);

        if (QueryParameters?.Count > 0)
        {
            string url = QueryStringBuilder.Append(request.RequestUri?.OriginalString ?? RequestUrlPath, QueryParameters);
            request.RequestUri = new Uri(url, UriKind.RelativeOrAbsolute);
        }
    }

    /// <summary>
    /// Sends a single attempt of <paramref name="request"/> and runs the exception and status code handlers.
    /// </summary>
    private async Task<HttpResponseMessage> SendAndHandle(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await SetAuthorizationHeader(request: request, cancellationToken: cancellationToken).ConfigureAwait(false);

        HttpResponseMessage response;
        Stopwatch reqWatch = Stopwatch.StartNew();
        try
        {
            response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            // Handlers observe the failure; there is no response to continue with, so it is always rethrown.
            _logger.LogWarning(ex, "{Method} {Url} failed after {Elapsed} ms", request.Method, request.RequestUri, reqWatch.ElapsedMilliseconds);
            await ExecuteExceptionHandlersInternal(null, ex, cancellationToken).ConfigureAwait(false);
            throw;
        }

        await WriteLog(response, reqWatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);

        // Only the method, url and status are logged: request headers carry credentials and bodies may carry personal data.
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("{Method} {Url} responded {StatusCode} in {Elapsed} ms",
                request.Method, request.RequestUri, (int)response.StatusCode, reqWatch.ElapsedMilliseconds);

            if (_logger.IsEnabled(LogLevel.Debug) && response.Content is not null)
            {
                string contents = await ReadAsStringAsync(response.Content, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Error response contents: {Contents}", contents);
            }
        }
        else
        {
            _logger.LogTrace("{Method} {Url} responded {StatusCode} in {Elapsed} ms",
                request.Method, request.RequestUri, (int)response.StatusCode, reqWatch.ElapsedMilliseconds);
        }

        await ExecuteStatusCodeHandlersInternal(response, cancellationToken).ConfigureAwait(false);

        return response;
    }

    /// <summary>
    /// Clones <paramref name="request"/> for a retry, giving it its own copy of the body.
    /// </summary>
    private static HttpRequestMessage CreateRetryRequest(HttpRequestMessage request, byte[]? body)
    {
        HttpRequestMessage retryRequest = Extensions.HttpRequestMessageExtensions.Clone(request);

        if (body is not null && request.Content is not null)
        {
            ByteArrayContent content = new(body);
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            retryRequest.Content = content;
        }

        return retryRequest;
    }

    /// <summary>
    /// Sends a GET request to <see cref="RequestUrlPath"/>, configured by the per-request delegates.
    /// </summary>
    private async Task<HttpResponseMessage> SendConfiguredRequest(CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, RequestUrlPath);
        return await ConfigAndSend(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a public entry point and clears the per-request configuration once the outermost one completes.
    /// </summary>
    private async Task<TResult> RunRequest<TResult>(Func<Task<TResult>> send)
    {
        _requestDepth++;
        try
        {
            return await send().ConfigureAwait(false);
        }
        finally
        {
            if (--_requestDepth == 0)
            {
                ClearConfigs();
            }
        }
    }

    protected virtual async Task<bool> ExecuteStatusCodeHandlersInternal(
        HttpResponseMessage? response,
        CancellationToken cancellationToken = default)
    {
        if (response is null || HandleStatusCodeBase is null)
        {
            return false;
        }

        // Obtain a snapshot of the registered handlers
        var handlers = HandleStatusCodeBase
            .GetInvocationList()
            .OfType<Func<HttpResponseMessage, CancellationToken, Task>>();

        foreach (var handler in handlers)
        {
            // Respect cancellation requests between handlers.
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogTrace("Executing status code handler {HandlerName}", handler.Method.Name);
            await handler(response, cancellationToken).ConfigureAwait(false);
            _logger.LogTrace("Executed status code handler {HandlerName}", handler.Method.Name);
        }

        return true;
    }


    protected virtual async ValueTask<bool> ExecuteExceptionHandlersInternal(
        HttpResponseMessage? response,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        if (HandleExceptionAsync is null)
        {
            return false;
        }

        // Retrieve a snapshot of registered exception handlers.
        var handlers = HandleExceptionAsync
            .GetInvocationList()
            .OfType<Func<HttpResponseMessage?, Exception, CancellationToken, Task>>();

        foreach (var handler in handlers)
        {
            // Check for cancellation between handler executions.
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogTrace("Executing exception handler {HandlerName}", handler.Method.Name);
            await handler(response, exception, cancellationToken).ConfigureAwait(false);
            _logger.LogTrace("Executed exception handler {HandlerName}", handler.Method.Name);
        }

        return true;
    }

    /// <summary>
    /// Resets the per-request configuration: delegates, handlers, url, query parameters, <see cref="EnsureSuccess"/>,
    /// and a <see cref="NamingStrategy"/> changed by a per-request configuration.
    /// </summary>
    protected virtual void ClearConfigs()
    {
        ConfigHttpClient = null;
        ConfigRequestMessage = null;
        HandleExceptionAsync = null;
        HandleStatusCodeBase = null;
        RequestUrlPath = string.Empty;
        QueryParameters.Clear();
        EnsureSuccess = false;

        if (_namingStrategyBeforeRequest is { } namingStrategy)
        {
            NamingStrategy = namingStrategy;
            _namingStrategyBeforeRequest = null;
        }
    }

    protected virtual async ValueTask<T?> Deserialize<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private static Task<string> ReadAsStringAsync(HttpContent content, CancellationToken cancellationToken) =>
#if NET5_0_OR_GREATER
        content.ReadAsStringAsync(cancellationToken);
#else
        content.ReadAsStringAsync();
#endif

    private static Task<Stream> ReadAsStreamAsync(HttpContent content, CancellationToken cancellationToken) =>
#if NET5_0_OR_GREATER
        content.ReadAsStreamAsync(cancellationToken);
#else
        content.ReadAsStreamAsync();
#endif

    private static Task<byte[]> ReadAsByteArrayAsync(HttpContent content, CancellationToken cancellationToken) =>
#if NET5_0_OR_GREATER
        content.ReadAsByteArrayAsync(cancellationToken);
#else
        content.ReadAsByteArrayAsync();
#endif

    #region IDisposable
    /// <summary>
    /// Releases this instance. The <see cref="HttpClient"/> passed to the constructor is owned by the caller
    /// (usually <see cref="IHttpClientFactory"/>) and is not disposed.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
    #endregion



#if NETSTANDARD2_0

    public virtual async Task<bool> ExecuteStatusCodeHandlers(HttpResponseMessage? response, CancellationToken cancellationToken)
    {
        return await ExecuteStatusCodeHandlersInternal(response, cancellationToken);
    }

    public virtual async Task<bool> ExecuteExceptionHandlers(HttpResponseMessage? response, Exception exception, CancellationToken cancellationToken)
    {
        return await ExecuteExceptionHandlersInternal(response, exception, cancellationToken);
    }

#endif

    /// <summary>
    /// Determines if we need to retry the request.
    /// </summary>
    /// <param name="response">Previously sent response</param>
    /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
    /// <returns>Returns a boolean indicates if retry needs to be done or not.</returns>
    protected virtual Task<bool> IsRetryRequired(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    /// <summary>
    /// Logs a message at the specified log level.
    /// </summary>
    /// <param name="logLevel">The level at which to log the message.</param>
    /// <param name="message">The message to be logged.</param>
    /// <param name="args">Optional arguments to be used in formatting the message.</param>
    public virtual void Log(LogLevel logLevel, string message, params object[] args)
    {
        _logger.Log(logLevel: logLevel, message: message, args: args);
    }
}

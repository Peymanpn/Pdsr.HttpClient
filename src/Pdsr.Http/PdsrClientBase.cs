using Microsoft.AspNetCore.WebUtilities;
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
    private bool _isRetrying;
    protected int _retryCount = 5;

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="client">HttpClient</param>
    /// <param name="loggerFactory">Instance of Logger factory</param>
    public PdsrClientBase(HttpClient client, ILoggerFactory loggerFactory)
    {
        _client = client;
        _logger = loggerFactory.CreateLogger<PdsrClientBase>();
        QueryParameters ??= new Dictionary<string, string?>();
    }

    #region Properties
    protected bool CallLogClientInteractions { get; set; } = false;

    public Action<HttpRequestMessage>? ConfigRequestMessage { get; set; }
    public Action<IPdsrClientBase>? ConfigHttpClient { get; set; }
    public IDictionary<string, string?> QueryParameters { get; set; }
    public string RequestUrlPath { get; set; } = "";
    public Func<HttpResponseMessage, CancellationToken, Task>? HandleStatusCodeBase { get; set; }
    public Func<HttpResponseMessage?, Exception, CancellationToken, Task>? HandleExceptionAsync { get; set; }

    /// <summary>
    /// If true, it throws an exception on any status lower than 200 and greater than 299.
    /// can be set by <see cref="Pdsr.Http.ClientExtensions.EnsureSuccess()"/>
    /// </summary>
    public bool EnsureSuccess { get; set; }

    /// <summary>
    /// Naming strategy to use while deserialize.
    /// default value <see cref="SerializationNamingStrategy.Camel"/>
    /// </summary>
    public SerializationNamingStrategy NamingStrategy { get; set; }

    #endregion


    /// <summary>
    /// Deserialize client output contents
    /// </summary>
    private protected virtual JsonSerializerOptions SerializerOptions => NamingStrategy switch
    {
        SerializationNamingStrategy.Camel => PdsrClientDefaults.CamelCaseSerializer,
        SerializationNamingStrategy.Snake => PdsrClientDefaults.SnakeSerializer,
        _ => PdsrClientDefaults.DefaultSerializer,
    };

    #region Client Capsulation

    public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) => _client.SendAsync(request);

    public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) => _client.SendAsync(request, cancellationToken);

    public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption) => _client.SendAsync(request, completionOption);

    public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken = default) => _client.SendAsync(request, completionOption, cancellationToken);

    protected virtual Uri? BaseAddress { get => _client.BaseAddress; set => _client.BaseAddress = value; }

    #endregion

    /// <inheritdoc/>
    public virtual async Task<string?> GetString(CancellationToken cancellationToken = default)
    {
        return await GetString(cancellationToken: cancellationToken, requestUrl: null, dontAuthenticate: true);
    }

    /// <inheritdoc/>
    public virtual async Task<Stream> GetStream(CancellationToken cancellationToken = default)
    {

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, RequestUrlPath);
        var response = await ConfigAndSend(request, cancellationToken);

        Stream stream = await response.Content.ReadAsStreamAsync();
        return stream;

    }

    /// <inheritdoc/>
    public virtual async Task<string?> GetString(CancellationToken cancellationToken = default, string? requestUrl = null, bool dontAuthenticate = false)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, RequestUrlPath);
        using var response = await ConfigAndSend(request, cancellationToken);

        string contentString = await response.Content.ReadAsStringAsync();
        return contentString;
    }

    /// <inheritdoc/>
    public virtual async Task<T?> SendAsync<T>(CancellationToken cancellationToken = default, string? requestUrl = null, bool dontAuthenticate = false)
    {
        using Stream stream = await GetStream(cancellationToken);
        try
        {
            T? content = await Deserialize<T>(stream, cancellationToken);
            return content;
        }
        catch (Exception)
        {
            if (EnsureSuccess)
            {
                throw;
            }
            return default;
        }
    }

    /// <inheritdoc/>
    public virtual async Task<T?> SendAsync<T>(CancellationToken cancellationToken = default)
    {
        var task = await SendAsync<T>(cancellationToken: cancellationToken, requestUrl: null, dontAuthenticate: false);
        return task;
    }

    /// <summary>
    /// Logic to Sets the base address.
    /// usually only for the first request per scope if it is null or not provided.
    /// </summary>
    protected abstract Task SetBaseAddress(CancellationToken cancellationToken = default);

    /// <summary>
    /// Add the required logic for authorization by implementing this method
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
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected abstract Task WriteLog(HttpResponseMessage response, long ellapsed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Main method to sends use the delegates and configures boths request and client.
    /// </summary>
    /// <returns>The response after sending the request</returns>
    protected virtual async Task<HttpResponseMessage> ConfigAndSend(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        await SetBaseAddress(cancellationToken);

        if (!_isRetrying)
        {
            ConfigRequestMessage?.Invoke(request);

            ConfigHttpClient?.Invoke(this);


            if (QueryParameters?.Count > 0)
            {
                var parsedQueries = QueryHelpers.AddQueryString(RequestUrlPath, QueryParameters);
                request.RequestUri = new Uri(parsedQueries, UriKind.Relative);
            }

        }

        await SetAuthorizationHeader(request: request, cancellationToken: cancellationToken);


        HttpResponseMessage response = new HttpResponseMessage();

        Stopwatch reqWatch = Stopwatch.StartNew();
        try
        {
            response = await this.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            if (!await ExecuteExceptionHandlersInternal(response, ex, cancellationToken))
            {
                response.Dispose();
                throw;
            }
        }

        await WriteLog(response, reqWatch.ElapsedMilliseconds, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("{statusCode}\nRequest:{@request}\nResponse: {@response}", response.StatusCode, request, response);

            var contents = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Error Response, contents: {contents}", contents);
        }
        else
        {
            _logger.LogTrace("{StatusCode}\nRequest: {@Request}\nResponse{@Response}", response.StatusCode, request, response);
        }


        await ExecuteStatusCodeHandlersInternal(response, cancellationToken);

        while (await IsRetryRequired(response, cancellationToken) && _retryCount > 0)
        {
            // clone the request
            var retryRequest = Extensions.HttpRequestMessageExtensions.Clone(request);

            _logger.LogInformation("Retrying the Request {url}, retries count remained: {retries}", retryRequest.RequestUri, _retryCount - 1);
            _logger.LogDebug("Retrying the Request, retries count remained: {retries}, previous status was: {status}" + Environment.NewLine +
                "request:{@request}" + Environment.NewLine +
                "response: {@response}", _retryCount - 1, response.StatusCode, request, response);

            // indicate that the consequent requests would be retries.
            _isRetrying = true;

            // decrement the counter
            _retryCount--;

            // --> retry
            _logger.LogTrace("Attempting to send retry request for cloned request {clonedRequest}", retryRequest);
            response = await ConfigAndSend(retryRequest, cancellationToken);
            _logger.LogDebug("Retry Request has been sent and got {retryStatus} with response {retryResponse}", response.StatusCode, response);
            // <-- retry

            return response;
        }

        ClearConfigs();

        _isRetrying = false;

        return response;

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



    protected private virtual void ClearConfigs()
    {
        ConfigHttpClient = null;
        ConfigRequestMessage = null;
        HandleExceptionAsync = null;
        HandleStatusCodeBase = null;
        RequestUrlPath = string.Empty;
    }

    protected private virtual async ValueTask<T?> Deserialize<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
    }

    #region IDisposable
    private bool disposed = false; // To detect redundant calls

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose managed resources.
            _client?.Dispose();
        }

        disposed = true;
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
    /// Determines if we need to resend the request.
    /// </summary>
    /// <param name="response">previously sent response</param>
    /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
    /// <returns>Returns a boolean indicates if retry needs to be done or not.</returns>
    protected virtual Task<bool> IsRetryRequired(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public virtual void Log(LogLevel logLevel, string message, params object[] args)
    {
        _logger.Log(logLevel, message, args);
    }
}

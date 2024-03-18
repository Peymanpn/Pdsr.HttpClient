using Microsoft.Extensions.Logging;

namespace Pdsr.Http;

#region Delegates

/// <summary>
/// Asynchronous delegate that takes <see cref="HttpResponseMessage"/>,
/// <see cref="HttpRequestMessage"/>,
/// <see cref="HttpStatusCode"/>,
/// and <see cref="CancellationToken"/> and returns a Task for the job to run.
/// </summary>
/// <param name="response">Response from an already send request</param>
/// <param name="request">Request from <see cref="HttpResponseMessage.RequestMessage"/></param>
/// <param name="statusCode">The status code of the underlying response</param>
/// <param name="cancellationToken"></param>
/// <returns></returns>
public delegate Task AsyncGeneralStatusHandler(HttpResponseMessage response, HttpRequestMessage? request, HttpStatusCode statusCode, CancellationToken cancellationToken = default);

/// <summary>
/// Asynchronous delegate that takes <see cref="HttpResponseMessage"/>,
/// <see cref="HttpRequestMessage"/>,
/// <see cref="HttpStatusCode"/> and returns a task for the job to run.
/// </summary>
/// <param name="response"></param>
/// <param name="request"></param>
/// <param name="statusCode"></param>
public delegate void GeneralStatusHandler(HttpResponseMessage response, HttpRequestMessage? request, HttpStatusCode statusCode);


public delegate Task ErrorRequestHandlerAsync(object? error, HttpResponseMessage response, CancellationToken cancellationToken = default);
public delegate void ErrorequestHandler(object? error, HttpResponseMessage response);

public delegate void ExceptionHandler(HttpResponseMessage? response, Exception exception);
public delegate Task ExceptionHandlerAsync(HttpResponseMessage? response, Exception exception, CancellationToken cancellationToken = default);

#endregion

/// <summary>
/// Custom HttpClient
/// </summary>
public interface IPdsrClientBase : IDisposable
{
    #region Props
    /// <summary>
    /// Configures a Request Message
    /// </summary>
    Action<HttpRequestMessage>? ConfigRequestMessage { get; set; }

    /// <summary>
    /// Configures the HttpClient.
    /// all configs will be applied before request.
    /// </summary>
    Action<IPdsrClientBase>? ConfigHttpClient { get; set; }

    /// <summary>
    /// Query Parameters to be added to request url
    /// </summary>
    IDictionary<string, string?> QueryParameters { get; set; }

    /// <summary>
    /// Request Url to added.
    /// </summary>
    string RequestUrlPath { get; set; }

    /// <summary>
    /// Holds the invocation list for status codes.
    /// should not be used directly. prefered to use through Extension methods in PdsrClientExtensions if
    /// the package Pdsr.HttpClient.Extensions is used.
    /// </summary>
    Func<HttpResponseMessage, CancellationToken, Task>? HandleStatusCodeBase { get; set; }

    /// <summary>
    /// Holds the invocation list for exceptions.
    /// </summary>
    Func<HttpResponseMessage?, Exception, CancellationToken, Task>? HandleExceptionAsync { get; set; }

    /// <summary>
    /// Ensures the response status code is in 200 - 299 range.
    /// it throws an exception otherwise
    /// </summary>
    bool EnsureSuccess { get; set; }

    public SerializationNamingStrategy NamingStrategy { get; set; }

    #endregion

    /// <summary>
    /// Configure the client and Get Contents as String
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string?> GetString(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the request and returns the bare stream from the response content.
    /// <see cref="HttpResponseMessage.Content"/>
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Stream> GetStream(CancellationToken cancellationToken = default);

    /// <summary>
    /// Configure the client and Get Response Contents as string
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <param name="requestUrl">request url to override <see cref="RequestUrlPath"/></param>
    /// <param name="dontAuthenticate">indicate to not use any Authorization Headers in the process</param>
    /// <returns></returns>
    Task<string?> GetString(CancellationToken cancellationToken = default, string? requestUrl = null, bool dontAuthenticate = false);

    /// <summary>
    /// Entry point Configure and send the request and then, Deserializes as T type. it will throw an exception if it can't deserialize
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="cancellationToken"></param>
    /// <param name="requestUrl"></param>
    /// <param name="dontAuthenticate"></param>
    /// <returns></returns>
    Task<T?> SendAsync<T>(CancellationToken cancellationToken = default, string? requestUrl = null, bool dontAuthenticate = false);

    /// <summary>
    /// Entry point to Configure and send the request and then, Deserializes as T type. it will throw an exception if it can't deserialize
    /// </summary>
    /// <typeparam name="T">type to deserialize response contentes</typeparam>
    /// <param name="cancellationToken"></param>
    /// <returns>Returns a Task representing the T Type</returns>
    Task<T?> SendAsync<T>(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the Status Code handlers added to delegate handler
    /// </summary>
    /// <param name="response"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
#if NETSTANDARD2_0
    Task<bool> ExecuteStatusCodeHandlers(HttpResponseMessage? response, CancellationToken cancellationToken = default);
#else
    async Task<bool> ExecuteStatusCodeHandlers(HttpResponseMessage? response, CancellationToken cancellationToken = default)
    {
        if (HandleStatusCodeBase is null) return false;
        var statusCodeHandlers = HandleStatusCodeBase.GetInvocationList();
        foreach (Func<HttpResponseMessage?, CancellationToken, Task> del in statusCodeHandlers)
        {
            await del(response, cancellationToken);
        }
        return true;
    }
#endif

    /// <summary>
    /// Executes the exception handlers added to the exception delegate handler
    /// </summary>
    /// <param name="response">Http Response message from an alredy send request</param>
    /// <param name="exception">the underlying exception</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
#if NETSTANDARD2_0
    Task<bool> ExecuteExceptionHandlers(HttpResponseMessage? response, Exception exception, CancellationToken cancellationToken = default);
#else
    async Task<bool> ExecuteExceptionHandlers(HttpResponseMessage? response, Exception exception, CancellationToken cancellationToken = default)
    {
        if (HandleExceptionAsync is null)
        {
            return false;
        }

        var exceptionHandler = HandleExceptionAsync.GetInvocationList();

        foreach (Func<HttpResponseMessage?, Exception, CancellationToken, Task> del in exceptionHandler)
        {
            await del(response, exception, cancellationToken);
        }

        return true;
    }
#endif

    /// <summary>
    /// Opens a Logger to outside to write Client based logs
    /// </summary>
    /// <param name="logLevel">Defines log security level</param>
    /// <param name="message">Formats and writes the log message.</param>
    /// <param name="args">an object array that contains zero or more objects to format.</param>
    void Log(LogLevel logLevel, string message, params object[] args);
}

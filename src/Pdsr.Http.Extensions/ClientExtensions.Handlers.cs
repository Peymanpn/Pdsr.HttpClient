namespace Pdsr.Http.Extensions;

public static partial class PdsrClientExtensions
{
    #region Handlers

    /// <summary>
    /// The <see cref="Func{HttpResponseMessage, CancellationToken, Task}"/> happens in any circumstance
    /// </summary>
    /// <typeparam name="TClient">Type of the client inherits from <see cref="IPdsrClientBase"/></typeparam>
    /// <param name="client">The underlying httpClient</param>
    /// <param name="handler">The Func to invoke</param>
    /// <returns>Retruns the same past client with the Func injected as delegate method</returns>
    public static TClient OnAnyResponse<TClient>(this TClient client, Func<HttpResponseMessage, CancellationToken, Task> handler)
        where TClient : IPdsrClientBase
    {
        client.ConfigClient(c => c.HandleStatusCodeBase += handler);
        return client;
    }

    /// <summary>
    /// The <see cref="Action{HttpResponseMessage}"/> happens in any circumstance
    /// </summary>
    /// <typeparam name="TClient">Type of the client inherits from <see cref="IPdsrClientBase"/></typeparam>
    /// <param name="client">The underlying httpClient</param>
    /// <param name="handler">The Action to invoke</param>
    /// <returns>Retruns the same past client with the Func injected as delegate method</returns>
    public static TClient OnAnyResponse<TClient>(this TClient client, Action<HttpResponseMessage> handler)
        where TClient : IPdsrClientBase
    {
        return client.OnAnyResponse((res, c) => { handler(res); return Task.CompletedTask; });
    }

    /// <summary>
    /// The <see cref="GeneralStatusHandler"/> invokation happens in any circumstance
    /// </summary>
    /// <typeparam name="TClient">Type of the client inherits from <see cref="IPdsrClientBase"/></typeparam>
    /// <param name="client">The underlying httpClient</param>
    /// <param name="handler">The Func to invoke</param>
    /// <returns>Retruns the same past client with the Func injected as delegate method</returns>
    public static TClient OnAnyResponse<TClient>(this TClient client, GeneralStatusHandler handler)
        where TClient : IPdsrClientBase
    {
        return client.OnAnyResponse((res, c) =>
        {
            handler(res, res.RequestMessage, res.StatusCode);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// The <see cref="AsyncGeneralStatusHandler"/> invokation happens in any circumstance
    /// </summary>
    /// <typeparam name="TClient">Type of the client inherits from <see cref="IPdsrClientBase"/></typeparam>
    /// <param name="client">The underlying httpClient</param>
    /// <param name="handler">The Func to invoke</param>
    /// <returns>Retruns the same past client with the Func injected as delegate method</returns>
    public static TClient OnAnyResponse<TClient>(this TClient client, AsyncGeneralStatusHandler handler)
        where TClient : IPdsrClientBase
    {
        return client.OnAnyResponse((res, c) =>
        {
            return handler(res, res.RequestMessage, res.StatusCode, c);
        });
    }


    #region BadRequest

    /// <summary>
    /// Returns the Deserialized TError Model from response.
    /// If the response contents is null, returns null
    /// </summary>
    /// <typeparam name="TError">Type of the model to deserialize</typeparam>
    /// <param name="response">an instance HttpResponseMessage containing server response message</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    internal static async ValueTask<TError?> GetBadRequestModel<TError>(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var stream = await response.Content.ReadAsStreamAsync(
#if NET5_0_OR_GREATER
                cancellationToken
#endif
            );
        var errorDto = await JsonSerializer.DeserializeAsync<TError>(stream, cancellationToken: cancellationToken);
        return errorDto;
    }

    public static TClient OnBadRequest<TClient, TError>(this TClient client, ErrorRequestHandlerAsync badRequestHandler)
        where TClient : IPdsrClientBase
    {
        return client.OnStatusCode(HttpStatusCode.BadRequest, async (res, c) =>
        {
            var errorDto = await GetBadRequestModel<TError>(res, c);
            await badRequestHandler(errorDto, res, c);
        });
    }

    public static TClient OnBadRequest<TClient>(this TClient client, ErrorRequestHandlerAsync badRequestHandler)
        where TClient : IPdsrClientBase
    {
        client.OnStatusCode(HttpStatusCode.BadRequest, async (res, c) =>
        {
            var errorDto = await GetBadRequestModel<object>(res, c);
            await badRequestHandler(errorDto, res, c);
        });
        return client;
    }

    public static TClient OnBadRequest<TClient>(this TClient client, ErrorequestHandler badRequestHandler)
        where TClient : IPdsrClientBase
    {
        client.OnStatusCode(HttpStatusCode.BadRequest, async (r, c) =>
        {
            var errorDto = await GetBadRequestModel<object>(r, c);
            badRequestHandler(errorDto, r);
        });
        return client;
    }

    public static TClient OnBadRequest<TClient>(this TClient client, Func<HttpResponseMessage, CancellationToken, Task> badRequestHandler)
        where TClient : IPdsrClientBase
    {
        return client.OnStatusCode(HttpStatusCode.BadRequest, (r, c) => badRequestHandler(r, c));
    }

    #endregion

    #region Exception
    public static TClient OnException<TClient>(this TClient client, ExceptionHandler exceptionHandler)
        where TClient : IPdsrClientBase
    {
        Func<HttpResponseMessage, Exception, CancellationToken, Task> handler = (r, ex, c) =>
        {
            exceptionHandler(r, ex);
            return Task.CompletedTask;
        };
        client.ConfigClient(c => c.HandleExceptionAsync += handler);
        return client;
    }


    public static TClient OnException<TClient>(this TClient client, ExceptionHandlerAsync exceptionHandlerAsync)
        where TClient : IPdsrClientBase
    {
        Func<HttpResponseMessage, Exception, CancellationToken, Task> exceptionHandler = async (r, ex, c) =>
        {
            await exceptionHandlerAsync(r, ex);
        };

        client.ConfigClient(c => c.HandleExceptionAsync += exceptionHandler);
        return client;
    }



    #endregion

    #region Status Handling


    public static TClient OnStatusCode<TClient>(this TClient client, HttpStatusCode statusCode, Func<HttpResponseMessage, CancellationToken, Task> handler)
        where TClient : IPdsrClientBase
    {
        client.Log(LogLevel.Trace, "Adding status handler for code {code}", statusCode);
        return client.OnAnyResponse(async (res, c) =>
        {
            if (((int)res.StatusCode) == ((int)statusCode))
            {
                client.Log(LogLevel.Debug, "Attempting to execute status handler code {code} for response {response}", res.StatusCode, res);
                await handler(res, c);
                client.Log(LogLevel.Debug, "Executed status handler code {code} for response {response}", res.StatusCode, res);
            }
        });
    }

    public static TClient OnStatusCode<TClient>(this TClient client, int statusCode, Func<HttpResponseMessage, CancellationToken, Task> handler)
    where TClient : IPdsrClientBase
    {
        client.Log(LogLevel.Trace, "Adding status handler for code {code}", statusCode);
        return client.OnAnyResponse(async (res, c) =>
        {
            if (((int)res.StatusCode) == statusCode)
            {
                client.Log(LogLevel.Debug, "Attempting to execute status handler code {Code} for response {Response}", res.StatusCode, res);
                await handler(res, c);
                client.Log(LogLevel.Debug, "Executed status handler code {code} for response {@Response}", res.StatusCode, res);
            }
        });
    }

    //public static TClient OnStatusCode<TClient>(this TClient client, GeneralStatusHandler handler)
    //    where TClient : IPdsrClientBase
    //{
    //    Func<HttpResponseMessage, CancellationToken, Task> handleInternal =
    //        (res, c) =>
    //        {
    //            handler(res, res.RequestMessage, res.StatusCode);
    //            return Task.CompletedTask;
    //        };
    //    client.ConfigClient(c => c.HandleStatusCodeBase += handleInternal);
    //    return client;
    //}

    //public static TClient OnStatusCode<TClient>(this TClient client, HttpStatusCode statusCode, Func<HttpResponseMessage, CancellationToken, Task> handler)
    //    where TClient : IPdsrClientBase
    //{
    //    Func<HttpResponseMessage, CancellationToken, Task> statusHandle = async (r, c) =>
    //     {
    //         if (r is not null && r.StatusCode == statusCode)
    //         {
    //             await handler(r, c);
    //         }
    //     };

    //    return client.ConfigClient(c => c.HandleStatusCodeBase += handler);
    //}

    //public static TClient OnStatusCode<TClient>(this TClient client, AsyncGeneralStatusHandler handler)
    //    where TClient : IPdsrClientBase
    // => client.OnAnyResponse((res, c) => handler(res, res.RequestMessage, res.StatusCode, c));

    public static TClient OnStatusCode<TClient>(this TClient client, Action<HttpResponseMessage, HttpStatusCode> handler)
        where TClient : IPdsrClientBase
    {
        client.OnAnyResponse((res, c) =>
        {
            handler(res, res.StatusCode);
            return Task.CompletedTask;
        });
        return client;
    }

    public static TClient OnStatusCode<TClient>(this TClient client, Action<HttpResponseMessage> handler, HttpStatusCode statusCode)
        where TClient : IPdsrClientBase
    {
        client.OnAnyResponse((res, req, status) =>
        {
            if (status == statusCode)
            {
                handler(res);
            }

        });
        return client;
    }

    public static TClient OnStatusCode<TClient>(this TClient client, HttpStatusCode statusCode, Action<HttpResponseMessage> handler)
        where TClient : IPdsrClientBase
    {
        client.OnAnyResponse((res, req, status) =>
        {
            if (status == statusCode)
            {
                handler(res);
            }

        });
        return client;
    }

    public static TClient OnStatusCode<TClient>(this TClient client, Action handler, HttpStatusCode statusCode)
        where TClient : IPdsrClientBase
    {
        client.OnAnyResponse((res, c) =>
        {
            if (res.StatusCode == statusCode)
            {
                handler();
            }
            return Task.CompletedTask;
        });
        return client;
    }

    public static TClient OnStatusCode<TClient>(this TClient client, HttpStatusCode statusCode, Action handler)
        where TClient : IPdsrClientBase
    {
        client.OnAnyResponse((res, c) =>
        {
            if (res.StatusCode == statusCode)
            {
                handler();
            }
            return Task.CompletedTask;
        });
        return client;
    }

    public static TClient OnNotFound<TClient>(this TClient client, Func<HttpResponseMessage?, CancellationToken, Task> handler)
        where TClient : IPdsrClientBase
    {
        return client.OnStatusCode(HttpStatusCode.NotFound, handler);
    }

    public static TClient OnForbidden<TClient>(this TClient client, Func<HttpResponseMessage, CancellationToken, Task> handleForbiddenResult)
        where TClient : IPdsrClientBase
    {
        return client.OnStatusCode(HttpStatusCode.Forbidden, handleForbiddenResult);
    }

    public static TClient OnTooManyRequests<TClient>(this TClient client, Func<HttpResponseMessage, CancellationToken, Task> handleTooManyRequests)
        where TClient : IPdsrClientBase
    {
        return client.OnStatusCode(429, handleTooManyRequests);
    }

    public static TClient OnAuthorizationFail<TClient>(this TClient client, GeneralStatusHandler handler)
        where TClient : IPdsrClientBase
    {
        return client.OnStatusCode(HttpStatusCode.Unauthorized, (res) => handler(res, res.RequestMessage, res.StatusCode));
    }

    public static TClient OnAuthorizationFail<TClient>(this TClient client, AsyncGeneralStatusHandler handler)
        where TClient : IPdsrClientBase
    {
        return client.OnStatusCode(HttpStatusCode.Unauthorized, (res, c) => handler(res, res.RequestMessage, res.StatusCode, c));
    }

    public static TClient OnAuthorizationFail<TClient>(this TClient client, Func<HttpResponseMessage, CancellationToken, Task> handler)
        where TClient : IPdsrClientBase
    {
        return client.OnStatusCode(HttpStatusCode.Unauthorized, (res, c) => handler(res, c));
    }

    public static TClient OnAuthorizationFail<TClient>(this TClient client, Action<HttpResponseMessage> handler)
        where TClient : IPdsrClientBase
    {
        return client.OnStatusCode(HttpStatusCode.Unauthorized, (res) => handler(res));
    }

    #endregion

    #region EnsureSuccess

    public static TClient EnsureSuccess<TClient>(this TClient client, Action<HttpResponseMessage, HttpStatusCode> whatToDoIfNoSuccessWithResponseMessage)
        where TClient : IPdsrClientBase
    {
        client.EnsureSuccess = true;
        client.OnAnyResponse((res, req, statusCode) =>
        {
            int code = (int)statusCode;
            if (code > 299 || code < 200)
            {
                whatToDoIfNoSuccessWithResponseMessage?.Invoke(res, statusCode);
                throw new HttpRequestException(
                    string.Format("Response status code does not indicate success: {0} ({1})", res.StatusCode, res.ReasonPhrase)
                        , inner: null);
            }
        });
        return client;
    }

    public static TClient EnsureSuccess<TClient>(this TClient client)
        where TClient : IPdsrClientBase
    {
        return client.EnsureSuccess(whatToDoIfNoSuccessWithResponseMessage: (r, s) =>
        {
            throw new HttpRequestException(
                string.Format("Response status code does not indicate success: {0} ({1})", r.StatusCode, r.ReasonPhrase)
                    , inner: null);
        });
    }

    public static TClient EnsureSuccess<TClient>(this TClient client, Action whatToDoIfNoSuccess)
        where TClient : IPdsrClientBase
    {
        client.EnsureSuccess = true;
        client.OnAnyResponse((res, req, statusCode) =>
        {
            int code = (int)statusCode;
            if (code > 299 || code < 200)
                whatToDoIfNoSuccess?.Invoke();
        });
        return client;
    }



    #endregion



    #endregion

}

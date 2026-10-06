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
    /// <returns>Returns the same past client with the Func injected as delegate method</returns>
    public static TClient OnAnyResponse<TClient>(this TClient client, Action<HttpResponseMessage> handler)
        where TClient : IPdsrClientBase
    {
        return client.OnAnyResponse((res, c) => { handler(res); return Task.CompletedTask; });
    }

    /// <summary>
    /// The handler, receiving the response, its request and status code, runs in any circumstance
    /// </summary>
    /// <typeparam name="TClient">Type of the client inherits from <see cref="IPdsrClientBase"/></typeparam>
    /// <param name="client">The underlying httpClient</param>
    /// <param name="handler">The Func to invoke</param>
    /// <returns>Returns the same past client with the Func injected as delegate method</returns>
    public static TClient OnAnyResponse<TClient>(this TClient client, Func<HttpResponseMessage, HttpRequestMessage?, HttpStatusCode, CancellationToken, Task> handler)
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
    /// <param name="namingStrategy">Naming strategy of the error model</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    internal static async ValueTask<TError?> GetBadRequestModel<TError>(HttpResponseMessage response, SerializationNamingStrategy namingStrategy, CancellationToken cancellationToken = default)
    {
        // Read from the buffered string rather than the content stream, which is cached and would be left at its end.
        string contents = await response.Content.ReadAsStringAsync(
#if NET5_0_OR_GREATER
                cancellationToken
#endif
            ).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(contents))
        {
            return default;
        }

        return JsonSerializer.Deserialize<TError>(contents, PdsrClientDefaults.GetSerializerOptions(namingStrategy));
    }

    public static TClient OnBadRequest<TClient, TError>(this TClient client, Func<TError?, HttpResponseMessage, CancellationToken, Task> badRequestHandler)
        where TClient : IPdsrClientBase
    {
        return client.OnStatusCode(HttpStatusCode.BadRequest, async (res, c) =>
        {
            var errorDto = await GetBadRequestModel<TError>(res, client.NamingStrategy, c);
            await badRequestHandler(errorDto, res, c);
        });
    }

    public static TClient OnBadRequest<TClient>(this TClient client, Func<object?, HttpResponseMessage, CancellationToken, Task> badRequestHandler)
        where TClient : IPdsrClientBase
    {
        client.OnStatusCode(HttpStatusCode.BadRequest, async (res, c) =>
        {
            var errorDto = await GetBadRequestModel<object>(res, client.NamingStrategy, c);
            await badRequestHandler(errorDto, res, c);
        });
        return client;
    }

    public static TClient OnBadRequest<TClient>(this TClient client, Action<object?, HttpResponseMessage> badRequestHandler)
        where TClient : IPdsrClientBase
    {
        client.OnStatusCode(HttpStatusCode.BadRequest, async (r, c) =>
        {
            var errorDto = await GetBadRequestModel<object>(r, client.NamingStrategy, c);
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
    public static TClient OnException<TClient>(this TClient client, Action<HttpResponseMessage?, Exception> exceptionHandler)
        where TClient : IPdsrClientBase
    {
        Func<HttpResponseMessage?, Exception, CancellationToken, Task> handler = (r, ex, _) =>
        {
            exceptionHandler(r, ex);
            return Task.CompletedTask;
        };
        client.ConfigClient(c => c.HandleExceptionAsync += handler);
        return client;
    }


    public static TClient OnException<TClient>(this TClient client, Func<HttpResponseMessage?, Exception, CancellationToken, Task> exceptionHandlerAsync)
        where TClient : IPdsrClientBase
    {
        Func<HttpResponseMessage?, Exception, CancellationToken, Task> exceptionHandler = async (r, ex, c) =>
        {
            await exceptionHandlerAsync(r, ex, c);
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

    public static TClient OnStatusCode<TClient>(this TClient client, Action<HttpResponseMessage, HttpStatusCode> handler)
        where TClient : IPdsrClientBase
    {
        client.OnAnyResponse((res, _) =>
        {
            handler(res, res.StatusCode);
            return Task.CompletedTask;
        });
        return client;
    }

    public static TClient OnStatusCode<TClient>(this TClient client, Action<HttpResponseMessage> handler, HttpStatusCode statusCode)
        where TClient : IPdsrClientBase
    {
        client.OnAnyResponse((res, _) =>
        {
            if (res.StatusCode == statusCode)
            {
                handler(res);
            }
            return Task.CompletedTask;

        });
        return client;
    }

    public static TClient OnStatusCode<TClient>(this TClient client, HttpStatusCode statusCode, Action<HttpResponseMessage> handler)
        where TClient : IPdsrClientBase
    {
        client.OnAnyResponse((res, _) =>
        {
            if (res.StatusCode == statusCode)
            {
                handler(res);
            }
            return Task.CompletedTask;
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

    public static TClient OnAuthorizationFail<TClient>(this TClient client, Action<HttpResponseMessage, HttpRequestMessage?, HttpStatusCode> handler)
        where TClient : IPdsrClientBase
    {
        return client.OnStatusCode(HttpStatusCode.Unauthorized, (res) => handler(res, res.RequestMessage, res.StatusCode));
    }

    public static TClient OnAuthorizationFail<TClient>(this TClient client, Func<HttpResponseMessage, HttpRequestMessage?, HttpStatusCode, CancellationToken, Task> handler)
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
        client.OnAnyResponse((res, _) =>
        {
            int code = (int)res.StatusCode;
            if (!res.IsSuccessStatusCode)
            {
                whatToDoIfNoSuccessWithResponseMessage.Invoke(res, res.StatusCode);
            }
            return Task.CompletedTask;
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
        client.OnAnyResponse((res, _) =>
        {
            if (!res.IsSuccessStatusCode)
            {
                whatToDoIfNoSuccess?.Invoke();
            }
            return Task.CompletedTask;
        });
        return client;
    }



    #endregion



    #endregion

}

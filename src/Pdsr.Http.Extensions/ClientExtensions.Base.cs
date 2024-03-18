using System.Net.Http.Headers;

namespace Pdsr.Http.Extensions;

/// <summary>
/// Contains Methods to prepare <see cref="IPdsrClientBase"/> and Requests
/// </summary>
public static partial class PdsrClientExtensions
{

    private const string _jsonMediaType = "application/json";

    /// <summary>
    /// Clears all Client and RequestMessage configs.
    /// It should happen if you no longer want to send the request with same the config
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <returns></returns>
    public static TClient ClearConfigs<TClient>(this TClient client)
        where TClient : IPdsrClientBase
    {
        client.ConfigHttpClient = null;
        client.ConfigRequestMessage = null;
        return client;
    }

    /// <summary>
    /// Removes all Exception and Status Code handlers attached to client to run.
    /// It should happen if you no longer want to send the request with same the config.
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <returns></returns>
    public static TClient ClearHandlers<TClient>(this TClient client)
        where TClient : IPdsrClientBase
    {

        client.HandleExceptionAsync = null;
        client.HandleStatusCodeBase = null;
        return client;
    }

    /// <summary>
    /// Removes all Client and Request configs.
    /// It should happen if you no longer want to send the request with same the config.
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <returns></returns>
    public static TClient Clear<TClient>(this TClient client)
        where TClient : IPdsrClientBase
    {
        client.ClearConfigs();
        client.ClearQueryStrings();
        client.ClearHandlers();
        client.RequestUrlPath = string.Empty;
        return client;
    }

    /// <summary>
    /// Removes QueryStrings.
    /// It should happen if you no longer want to send the request with same the config
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <returns></returns>
    public static TClient ClearQueryStrings<TClient>(this TClient client)
        where TClient : IPdsrClientBase
    {
        if (client.QueryParameters == null)
            client.QueryParameters = new Dictionary<string, string?>();
        else
            client.QueryParameters.Clear();
        return client;
    }


    /// <summary>
    /// Checks if specified query string exists in config
    /// </summary>
    /// <typeparam name="TClient">TClient typed client</typeparam>
    /// <param name="client">HttpClient to apply config on</param>
    /// <param name="queryStringKey"></param>
    /// <returns></returns>
    public static bool HasQueryString<TClient>(this TClient client, string queryStringKey)
        where TClient : IPdsrClientBase => client.QueryParameters.ContainsKey(queryStringKey);

    /// <summary>
    /// Adds a query string to request config
    /// </summary>
    /// <typeparam name="TClient">TClient typed client</typeparam>
    /// <param name="client">HttpClient to apply config on</param>
    /// <param name="key">query string key</param>
    /// <param name="value">Query String value</param>
    /// <returns>returns HttpClient of typed client/></returns>
    public static TClient AddQueryString<TClient>(this TClient client, string key, string value)
        where TClient : IPdsrClientBase
    {

        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException($"'{nameof(key)}' cannot be null or empty.", nameof(key));
        }

        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException($"'{nameof(value)}' cannot be null or empty.", nameof(value));
        }

        if (client.QueryParameters == null) client.QueryParameters = new Dictionary<string, string?>();
        if (client.QueryParameters.ContainsKey(key))
        {
            if (client.QueryParameters.Remove(key))
                client.QueryParameters.Add(key, value);

            else
                throw new Exception("Cannot remove a previously added query string.");
        }
        else
        {
            client.QueryParameters.Add(key, value);
        }
        return client;
    }

    /// <summary>
    /// Adds a query string to the request.
    /// If the object has other types than string, it will stringify it with <see cref="object.ToString"/>
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <param name="key">QueryString's key</param>
    /// <param name="objectValue">QueryString's Value as object</param>
    /// <returns></returns>
    public static TClient AddQueryString<TClient>(this TClient client, string key, object objectValue)
        where TClient : IPdsrClientBase
            => client.AddQueryString(key, value:
#if NETSTANDARD2_0
                objectValue.ToString()
#else
                objectValue.ToString() ?? throw new NullReferenceException(nameof(objectValue))
#endif
                );

    /// <summary>
    /// Adds a query string to the request with the provided keyvalue pairs.
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <param name="queryString"></param>
    /// <returns></returns>
    public static TClient AddQueryString<TClient>(this TClient client, KeyValuePair<string, string> queryString)
        where TClient : IPdsrClientBase
            => client.AddQueryString(queryString.Key, queryString.Value);

    /// <summary>
    /// Adds a delegate to the <see cref="HttpRequestMessage"/> invokes list. the delegate <see cref="Action"/> will run right before sending the request.
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <param name="configRequest"></param>
    /// <returns></returns>
    public static TClient ConfigRequest<TClient>(this TClient client, Action<HttpRequestMessage> configRequest)
        where TClient : IPdsrClientBase
    {
        client.ConfigRequestMessage += configRequest;
        return client;
    }

    /// <summary>
    /// Adds a delegate to the <see cref="TClient"/> invocation list.
    /// It runs right before sending the request.
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <param name="configClient"></param>
    /// <returns></returns>
    public static TClient ConfigClient<TClient>(this TClient client, Action<IPdsrClientBase> configClient)
        where TClient : IPdsrClientBase
    {
        client.ConfigHttpClient += configClient;
        return client;
    }

    /// <summary>
    /// Replaces the provided AcceptType as header to the RequestMessage
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <param name="acceptType"></param>
    /// <returns></returns>
    public static TClient Accept<TClient>(this TClient client, string acceptType = _jsonMediaType)
        where TClient : IPdsrClientBase
    {
        return client.ConfigRequest(c => c.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptType)));
    }

    /// <summary>
    /// Sets the AcceptType to application/json.
    /// shorthand for Accept(client,"application/json")
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <returns></returns>
    public static TClient AcceptJson<TClient>(this TClient client)
        where TClient : IPdsrClientBase
    {
        client.ConfigRequest(c =>
        {
            c.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(_jsonMediaType));
        });
        return client;
    }
}

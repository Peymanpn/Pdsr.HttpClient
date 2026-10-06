using System.Text;

namespace Pdsr.Http.Extensions;

public static partial class PdsrClientExtensions
{
    /// <summary>
    /// Prepares a Get Request
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <returns></returns>
    public static TClient Get<TClient>(this TClient client)
       where TClient : IPdsrClientBase
            => client.ConfigRequest(c => c.Method = HttpMethod.Get);

    /// <summary>
    /// Prepares a POST Request
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <param name="data"></param>
    /// <param name="namingStrategy"></param>
    /// <param name="mediaType"></param>
    /// <returns></returns>
    public static TClient Post<TClient>(this TClient client, object data, SerializationNamingStrategy namingStrategy = SerializationNamingStrategy.Camel, string mediaType = _jsonMediaType)
        where TClient : IPdsrClientBase
    {
        client.ConfigRequest(config =>
        {
            config.Method = HttpMethod.Post;
            SerializeAndGetContent(data, namingStrategy, mediaType, config);
        });
        return client;
    }

    /// <summary>
    /// Prepares  PUT request
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <param name="data"></param>
    /// <param name="namingStrategy"></param>
    /// <param name="mediaType"></param>
    /// <returns></returns>
    public static TClient Put<TClient>(this TClient client, object data, SerializationNamingStrategy namingStrategy = SerializationNamingStrategy.Camel, string mediaType = _jsonMediaType)
        where TClient : IPdsrClientBase
    {
        return client.ConfigRequest(config =>
        {
            config.Method = HttpMethod.Put;
            SerializeAndGetContent(data, namingStrategy, mediaType, config);
        });
    }


    /// <summary>
    /// Prepares a DELETE Request
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <returns></returns>
    public static TClient Delete<TClient>(this TClient client)
        where TClient : IPdsrClientBase
    {
        client.ConfigRequest(config =>
        {
            config.Method = HttpMethod.Delete;
        });
        return client;
    }

    /// <summary>
    /// Patch with serilizing provided data.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="data"></param>
    /// <param name="namingStrategy"></param>
    /// <returns></returns>
    public static TClient Patch<TClient>(this TClient client, object data, SerializationNamingStrategy namingStrategy = SerializationNamingStrategy.Camel, string mediaType = _jsonMediaType)
        where TClient : IPdsrClientBase
    {
        client.ConfigRequest(req =>
        {
#if NETSTANDARD2_0
            req.Method = new HttpMethod("PATCH");
#else
            req.Method = HttpMethod.Patch;
#endif
            SerializeAndGetContent(data, namingStrategy, mediaType, req);
        });
        return client;
    }

    /// <summary>
    /// Sets the Request Contents serialization to CamelCase
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <returns></returns>
    public static TClient CamelCase<TClient>(this TClient client)
        where TClient : IPdsrClientBase
    {
        return client.ConfigClient(c => c.NamingStrategy = SerializationNamingStrategy.Camel);
    }

    /// <summary>
    /// Sets the Contents serialization to SnakeCase
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <returns></returns>
    public static TClient SnakeCase<TClient>(this TClient client)
        where TClient : IPdsrClientBase
    {
        return client.ConfigClient(c => c.NamingStrategy = SerializationNamingStrategy.Snake);
    }

    /// <summary>
    /// locally serialize
    /// </summary>
    /// <param name="data"></param>
    /// <param name="namingStrategy"></param>
    /// <param name="mediaType"></param>
    /// <param name="request"></param>
    private static void SerializeAndGetContent(object data, SerializationNamingStrategy namingStrategy, string mediaType, HttpRequestMessage request)
    {
        JsonSerializerOptions? serializationSettings = namingStrategy switch
        {
            SerializationNamingStrategy.None => PdsrClientDefaults.DefaultSerializer,
            SerializationNamingStrategy.Camel => PdsrClientDefaults.CamelCaseSerializer,
            SerializationNamingStrategy.Snake => PdsrClientDefaults.SnakeSerializer,
            _ => PdsrClientDefaults.DefaultSerializer,
        };

        string contents = JsonSerializer.Serialize(data, serializationSettings);
        request.Content = new StringContent(contents, Encoding.UTF8, mediaType);
    }
}

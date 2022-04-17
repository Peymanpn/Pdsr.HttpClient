namespace Pdsr.Http.Extensions;

public static class HttpRequestMessageExtensions
{
    /// <summary>
    /// Clones an already sent request to be retried later
    /// </summary>
    /// <param name="request">a Request Message that already been sent.</param>
    /// <returns>Cloned request with same values as original request</returns>
    public static HttpRequestMessage Clone(this HttpRequestMessage request)
    {
        HttpRequestMessage clone = new(request.Method, request.RequestUri);

        clone.Content = request.Content;
        clone.Version = request.Version;

#if NETSTANDARD2_0
        foreach (KeyValuePair<string, object> prop in request.Properties)
        {
            clone.Properties.Add(prop);
        }
#elif NETSTANDARD2_1
        foreach (KeyValuePair<string, object> prop in request.Properties)
        {
            clone.Properties.Add(prop);
        }
#elif NETCOREAPP3_1
        foreach (KeyValuePair<string, object> prop in request.Properties)
        {
            clone.Properties.Add(prop);
        }
#elif NET5_0
        foreach (KeyValuePair<string, object?> prop in request.Options)
        {
            clone.Options.TryAdd(prop.Key, prop.Value);
        }
#elif NET6_0_OR_GREATER
        foreach (KeyValuePair<string, object?> prop in request.Options)
        {
            clone.Options.TryAdd(prop.Key, prop.Value);
        }
#else
        foreach (KeyValuePair<string, object> prop in request.Properties)
        {
            clone.Properties.Add(prop);
        }
#endif

        // copying headers
        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}

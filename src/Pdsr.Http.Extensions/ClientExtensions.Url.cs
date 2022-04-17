namespace Pdsr.Http.Extensions;

public static partial class PdsrClientExtensions
{
    #region Url

    /// <summary>
    /// sets the Url
    /// </summary>
    /// <typeparam name="TClient">IPdsrClientBase to add url to</typeparam>
    /// <param name="client">HttpClient to add url to</param>
    /// <param name="url">Url</param>
    /// <param name="append">should append or replace</param>
    /// <returns></returns>
    public static TClient Url<TClient>(this TClient client, string url, bool append = false)
        where TClient : IPdsrClientBase
        => append ? client.SetUrl(url) : client.AddUrl(url);

    /// <summary>
    /// Sets or Append the url for request.RequestUrl
    /// </summary>
    /// <param name="client"></param>
    /// <param name="url"></param>
    /// <param name="append"></param>
    /// <returns></returns>
    public static IPdsrClientBase Url(this IPdsrClientBase client, string url, bool append = false)
        => append ? client.SetUrl(url) : client.AddUrl(url);

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <param name="url"></param>
    /// <param name="makeAbsolute">Make Absolute path if not ends with '/'</param>
    /// <returns></returns>
    public static TClient SetUrl<TClient>(this TClient client, string url, bool makeAbsolute = false)
        where TClient : IPdsrClientBase
    {
        client.RequestUrlPath = url;
        if (makeAbsolute && !client.RequestUrlPath.EndsWith("/")) client.RequestUrlPath += '/';
        return client;
    }

    /// <summary>
    /// Add url to the Request Url Path
    /// </summary>
    /// <typeparam name="TClient"></typeparam>
    /// <param name="client"></param>
    /// <param name="url"></param>
    /// <param name="makeAbsolute">Append an '/' at the end if doesn't have</param>
    /// <returns></returns>
    public static TClient AddUrl<TClient>(this TClient client, string url, bool makeAbsolute = false)
        where TClient : IPdsrClientBase
    {
        if (!string.IsNullOrEmpty(client.RequestUrlPath) && !client.RequestUrlPath.EndsWith("/")) client.RequestUrlPath += "/";
        client.RequestUrlPath += url;
        if (makeAbsolute && client.RequestUrlPath.EndsWith("/")) client.RequestUrlPath += '/';
        return client;
    }

    #endregion
}

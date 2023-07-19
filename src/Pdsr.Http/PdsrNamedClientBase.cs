using Microsoft.Extensions.Logging;

namespace Pdsr.Http;

/// <summary>
/// Abstraction of <see cref="PdsrClientBase"/> with named clients
/// </summary>
/// <typeparam name="TConfig"><see cref="IPdsrClientConfigs"/> type for client configurations</typeparam>
public abstract class PdsrNamedClientBase<TConfig> : PdsrClientBase
    where TConfig : IPdsrClientConfigs
{
    /// <inheritdoc/>
    protected PdsrNamedClientBase(HttpClient client, ILoggerFactory loggerFactory)
        : base(client, loggerFactory)
    {
    }


    /// <summary>
    /// Using <see cref="IHttpClientFactory"/> instead of using unnamed <see cref="HttpClient" />
    /// To Use the named client, the type <see cref="PdsrClientConfigs"/> must be injected to DI
    /// and <see cref="PdsrClientConfigs.ClientName"/> must be initialized.
    /// </summary>
    /// <param name="httpClientFactory">Client Factory, with named clients</param>
    /// <param name="loggerFactory">Instance of Logger factory</param>
    /// <param name="clientConfigs">PdsrClientConfigurations, must be injected to the DI if you plan to use named clients</param>
    public PdsrNamedClientBase(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, TConfig clientConfigs)
        : base(httpClientFactory.CreateClient(clientConfigs.ClientName), loggerFactory)
    {
    }

}

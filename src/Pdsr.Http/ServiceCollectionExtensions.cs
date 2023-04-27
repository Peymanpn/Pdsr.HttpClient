using Microsoft.Extensions.DependencyInjection;

namespace Pdsr.Http;

/// <summary>
/// Adds and configures required services and configs to the DI container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register an instance of HttpClient with default name
    /// <see cref="PdsrClientDefaults.DefaultClientName"/>
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="clientConfigs">HttpClient configurations</param>
    /// <returns>an Instance of <see cref="IHttpClientBuilder"/> with registerd <see cref="HttpClient"/></returns>
    public static IHttpClientBuilder AddPdsrClient(this IServiceCollection services, IPdsrClientConfigs? clientConfigs)
    {
        clientConfigs ??= new PdsrClientConfigs();

        var builder = AddPdsrClient<IPdsrClientConfigs>(services, clientConfigs);

        return builder;
    }


    /// <summary>
    /// Register an instance of HttpClient with custom <typeparamref name="TConfig"/> as config type
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="clientConfigs">HttpClient configurations</param>
    /// <returns>an Instance of <see cref="IHttpClientBuilder"/> with registerd <see cref="HttpClient"/></returns>
    public static IHttpClientBuilder AddPdsrClient<TConfig>(this IServiceCollection services, TConfig clientConfigs)
        where TConfig : IPdsrClientConfigs
    {
        var builder = services.AddHttpClient(clientConfigs.ClientName);

        return builder;
    }
}

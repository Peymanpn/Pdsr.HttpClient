using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;

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
    /// <returns>an Instance of <see cref="IHttpClientBuilder"/> with registered <see cref="HttpClient"/></returns>
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
    /// <returns>an Instance of <see cref="IHttpClientBuilder"/> with registered <see cref="HttpClient"/></returns>
    public static IHttpClientBuilder AddPdsrClient<TConfig>(this IServiceCollection services, TConfig clientConfigs)
        where TConfig : IPdsrClientConfigs
    {
        var builder = services.AddHttpClient(clientConfigs.ClientName);

        return builder;
    }

    public static IHttpClientBuilder AddPdsrClient<TClientInterface, TClient>(this IServiceCollection services, IPdsrClientConfigs? clientConfigs = null)
        where TClientInterface : class
        where TClient : class, TClientInterface
    {
        clientConfigs ??= new PdsrClientConfigs();

        services.AddScoped<TClientInterface, TClient>();
        return services.AddPdsrClient(clientConfigs);
    }
}

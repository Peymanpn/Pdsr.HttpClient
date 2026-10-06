using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// Register a named HttpClient using <paramref name="clientConfigs"/>,
    /// and registers <paramref name="clientConfigs"/> as <typeparamref name="TConfig"/> for <see cref="PdsrNamedClientBase{TConfig}"/>.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="clientConfigs">HttpClient configurations</param>
    /// <returns>an Instance of <see cref="IHttpClientBuilder"/> with registered <see cref="HttpClient"/></returns>
    public static IHttpClientBuilder AddPdsrClient<TConfig>(this IServiceCollection services, TConfig clientConfigs)
        where TConfig : IPdsrClientConfigs
    {
        services.TryAdd(ServiceDescriptor.Singleton(typeof(TConfig), clientConfigs));

        var builder = services.AddHttpClient(clientConfigs.ClientName);

        return builder;
    }

    /// <summary>
    /// Registers <typeparamref name="TClient"/> as a typed client of <typeparamref name="TClientInterface"/>,
    /// receiving the named HttpClient configured by the returned builder.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="clientConfigs">HttpClient configurations. When null, the client is named after <typeparamref name="TClient"/>.
    /// A client name can only be bound to one typed client.</param>
    /// <returns>an Instance of <see cref="IHttpClientBuilder"/> to configure the client's <see cref="HttpClient"/></returns>
    public static IHttpClientBuilder AddPdsrClient<TClientInterface, TClient>(this IServiceCollection services, IPdsrClientConfigs? clientConfigs = null)
        where TClientInterface : class
        where TClient : class, TClientInterface
    {
        if (clientConfigs is null)
        {
            return services.AddHttpClient<TClientInterface, TClient>();
        }

        services.TryAddSingleton(clientConfigs);

        return services.AddHttpClient<TClientInterface, TClient>(clientConfigs.ClientName);
    }
}

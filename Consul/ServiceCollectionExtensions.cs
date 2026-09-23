using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ServiceDiscovery;
using service_discovery.Consul._Internal;

namespace service_discovery.Consul;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConsulServiceDiscovery(this IServiceCollection services, 
        IConfiguration configuration, 
        string consulSectionName = "consulServiceDiscoverySettings")
    {
        var serviceDiscoverySettings = configuration.GetSection(consulSectionName).Get<ConsulServiceDiscoverySettings>();
        
        if (serviceDiscoverySettings is null)
            throw new Exception("Can not find Consul service discovery settings.");
        
        services.AddSingleton<ConsulServiceDiscoverySettings>(serviceDiscoverySettings);
        
        services.AddConsulClient(serviceDiscoverySettings.ConnectionSettings);
        
        services.AddSingleton<IServiceEndpointProviderFactory, ConsulServiceEndpointProviderFactory>();

        if (serviceDiscoverySettings.PassThroughAfterDiscoveryFailure)
        {
            // Attempt to create the endpoint "as-is".
            // Suitable for cases where other service discovery providers cannot resolve service addresses.
            services.AddPassThroughServiceEndpointProvider();
        };
        
        return services;
    }

    private static IServiceCollection AddConsulClient(this IServiceCollection services, ConsulConnectionSettings connectionSettings)
    {
        services.AddSingleton<IConsulClient, ConsulClient>(_ => new ConsulClient(consulConfig =>
        {
            consulConfig.Address = connectionSettings.Uri;
            
            if (connectionSettings.Token is not null)
                consulConfig.Token = connectionSettings.Token;
        }));
        
        return services;
    }
}
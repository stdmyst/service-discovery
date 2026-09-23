using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;

namespace service_discovery.Consul._Internal;

internal sealed class ConsulServiceEndpointProvider(
    ServiceEndpointQuery query, 
    ConsulServiceWatcher watcher, 
    ILoggerFactory loggerFactory) : IServiceEndpointProvider
{
    private readonly ILogger<ConsulServiceEndpointProvider> _logger = loggerFactory.CreateLogger<ConsulServiceEndpointProvider>();
    
    public async ValueTask PopulateAsync(IServiceEndpointBuilder endpoints, CancellationToken cancellationToken)
    {
        var serviceSnapshot = await watcher.GetHealthyServiceAddresses(cancellationToken);
        
        endpoints.AddChangeToken(serviceSnapshot.ChangeToken);
        
        foreach (var serviceAddress in serviceSnapshot.Addresses)
        {
            if (ServiceEndpoint.TryParse(serviceAddress, out var serviceEndpoint))
            {
                endpoints.Endpoints.Add(serviceEndpoint);
            }
            else
            {
                _logger.LogError("Failed to parse {ServiceName} service address: {ServiceAddress}", query.ServiceName, serviceAddress);
            }
        }
    }
    
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
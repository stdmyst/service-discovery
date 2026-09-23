using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Consul;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;

namespace service_discovery.Consul._Internal;

internal sealed class ConsulServiceEndpointProviderFactory(
    IConsulClient consulClient, 
    ILoggerFactory loggerFactory, 
    ConsulServiceDiscoverySettings serviceDiscoverySettings) 
    : IServiceEndpointProviderFactory, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ConsulServiceWatcher> _watchers = new();
    
    public bool TryCreateProvider(ServiceEndpointQuery query, [NotNullWhen(true)] out IServiceEndpointProvider? provider)
    {
        var watcher = _watchers.GetOrAdd(query.ServiceName, 
            _ => new ConsulServiceWatcher(query.ServiceName, consulClient, serviceDiscoverySettings, loggerFactory));
        
        provider = new ConsulServiceEndpointProvider(query, watcher, loggerFactory);
        
        return true;
    }
    
    public async ValueTask DisposeAsync()
    {
        foreach (var worker in _watchers.Values)
            await worker.DisposeAsync().ConfigureAwait(false);
    }
}
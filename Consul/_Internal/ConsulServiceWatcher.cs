using Consul;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace service_discovery.Consul._Internal;

internal sealed class ConsulServiceWatcher : IAsyncDisposable
{
    private readonly string _serviceName;
    private readonly IConsulClient _consulClient;
    private readonly ConsulServiceDiscoverySettings _serviceDiscoverySettings;
    private readonly TimeSpan _onFailureDelay;
    private readonly ILogger<ConsulServiceWatcher> _logger;
    
    private readonly TaskCompletionSource _synchronization = new();
    
    private readonly CancellationTokenSource _shutdown = new();
    private CancellationTokenSource _changeTokenSource = new();
    
    private readonly Task _worker;
    
    private ConsulServiceSnapshot _consulServiceSnapshot;

    public ConsulServiceWatcher(
        string serviceName, 
        IConsulClient consulClient, 
        ConsulServiceDiscoverySettings serviceDiscoverySettings,
        ILoggerFactory loggerFactory)
    {
        _serviceName = serviceName;
        _consulClient = consulClient;
        _serviceDiscoverySettings = serviceDiscoverySettings;
        _onFailureDelay = TimeSpan.FromSeconds(serviceDiscoverySettings.OnFailureDelaySeconds);
        _logger = loggerFactory.CreateLogger<ConsulServiceWatcher>();
        
        _consulServiceSnapshot = new ConsulServiceSnapshot([], new CancellationChangeToken(_changeTokenSource.Token));
        
        _worker = Task.Run(Watch, _shutdown.Token);
    }

    public async ValueTask<ConsulServiceSnapshot> GetHealthyServiceAddresses(CancellationToken cancellationToken)
    {
        if (!_synchronization.Task.IsCompleted)
        {
            await _synchronization.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        
        return _consulServiceSnapshot;
    }

    private async Task Watch()
    {
        var trackingIndex = 0UL;
        
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                var queryOptions = new QueryOptions
                {
                    WaitIndex = trackingIndex,
                    WaitTime = TimeSpan.FromSeconds(_serviceDiscoverySettings.WaitTimeSeconds)
                };
                
                var queryResult = await _consulClient.Health
                    .Service(_serviceName, tag: null, _serviceDiscoverySettings.PassingOnly, queryOptions, _shutdown.Token)
                    .ConfigureAwait(false);
                
                trackingIndex = ComputeNewIndex(trackingIndex, queryResult);
                
                var addresses = queryResult.Response
                    .Select(x =>
                    {
                        var address = x.Service.Address;
                        return x.Service.Port > 0 ? $"{address}:{x.Service.Port}" : address;
                    })
                    .Order()
                    .ToArray();
                
                if (_synchronization.Task.IsCompleted && !HaveTheAddressesChanged(addresses))
                    continue;
                
                var hasChangesTokenSource = Interlocked.Exchange(ref _changeTokenSource, new CancellationTokenSource());
                
                var changeToken = new CancellationChangeToken(_changeTokenSource.Token);
                
                _consulServiceSnapshot = new ConsulServiceSnapshot(addresses, changeToken);
                
                _synchronization.TrySetResult();
                
                // Cancel all source-related change tokens to notify consumers about updates.
                await hasChangesTokenSource.CancelAsync();
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                _logger.LogError("An exception occurred while resolving {ServiceName} service addresses via Consul. " + 
                                 "The latest snapshot will be used. " + 
                                 "The next resolution attempt will be made after the {DelayInterval} interval.\n" + 
                                 "{Exception}",
                    _serviceName, _onFailureDelay, e.Message);
                
                _synchronization.TrySetResult();
                
                try
                {
                    await Task.Delay(_onFailureDelay, _shutdown.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    // SEE: https://developer.hashicorp.com/consul/api-docs/features/blocking
    private static ulong ComputeNewIndex(ulong trackingIndex, QueryResult queryResult)
    {
        var queryResultIndex = queryResult.LastIndex;
        
        if (queryResultIndex < 1)
            return 1;
        
        if (queryResultIndex < trackingIndex)
            return 0;   
        
        return queryResultIndex;
    }

    private bool HaveTheAddressesChanged(string[] addresses) 
        => !addresses.SequenceEqual(_consulServiceSnapshot.Addresses);

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync().ConfigureAwait(false);
        
        try
        {
            await _worker.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        
        _shutdown.Dispose();    
    }
}
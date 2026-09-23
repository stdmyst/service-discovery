namespace service_discovery.Consul;

public class ConsulServiceDiscoverySettings
{
    public required ConsulConnectionSettings ConnectionSettings { get; init; }
    public bool PassingOnly { get; init; } = true;
    public uint WaitTimeSeconds { get; init; } = 60;
    public uint OnFailureDelaySeconds { get; init; } = 60;
    public bool PassThroughAfterDiscoveryFailure { get; init; } = false;
}

public class ConsulConnectionSettings
{
    public required Uri Uri { get; init; }
    public string? Token { get; init; }
}
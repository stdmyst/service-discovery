using Microsoft.Extensions.Primitives;

namespace service_discovery.Consul;

public sealed record ConsulServiceSnapshot(IReadOnlyCollection<string> Addresses, IChangeToken ChangeToken);
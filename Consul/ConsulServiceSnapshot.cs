using Microsoft.Extensions.Primitives;

namespace service_discovery.Consul;

public sealed record ConsulServiceSnapshot(IEnumerable<string> Addresses, IChangeToken ChangeToken);
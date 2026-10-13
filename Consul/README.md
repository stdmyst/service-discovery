Implements Consul Service Discovery functionality using `Microsoft.Extensions.ServiceDiscovery` abstractions.

For each service, a worker is registered that monitors service address changes via Consul Blocking-Query and provides support to consumers through the `IChangeToken` interface.

## Examples

### Named `HttpClient` and Service Discovery

```csharp
using Microsoft.AspNetCore.Mvc;
using Platform.ServiceDiscovery.Consul;

const string catalogApi = "catalog";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConsulServiceDiscovery(builder.Configuration);

builder.Services.AddHttpClient($"{catalogApi}-client", configureClient: client =>
{
    // It will be automatically resolved with actual address during service discovery.
    client.BaseAddress = new Uri($"https://{catalogApi}");
}).AddServiceDiscovery();

var app = builder.Build();

app.MapGet("catalog/ping", async ([FromServices] IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient($"{catalogApi}-client");
    var response = await client.GetAsync($"/_/status");

    return response.StatusCode;
});

app.Run();
```

Settings example:

```json
{
  "ConsulServiceDiscoverySettings": {
    "ConnectionSettings": {
      "Uri": "https://{$CONSUL_ADDRESS}"
    },
    "PassingOnly": true,
    "WaitTimeSeconds": 60,
    "OnFailureDelaySeconds": 60,
    "PassThroughAfterDiscoveryFailure": false
  }
}
```
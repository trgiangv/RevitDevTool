using DevTools.Mcp.Adapter;
using DevTools.Mcp.Adapter.External;
using DevTools.Mcp.Adapter.Tests.Harness;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Mcp.Adapter.Tests;

public sealed class HostAdapterRegistrationTests
{
    [Fact]
    public void AddMcpHostAdapter_RegistersHostPipeServer()
    {
        var services = new ServiceCollection();
        services.AddMcpHostAdapter();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(HostMcpPipeServer));
    }

    [Fact]
    public async Task HostDiscover_AdvertisesListChanged()
    {
        var handler = McpHostTestHarness.CreateHandler(McpHostTestHarness.CreateCatalogStore());
        var response = await handler.HandleAsync(
            McpHostTestHarness.CreateDiscoverRequest(),
            TestContext.Current.CancellationToken);

        var json = response!["result"]!.AsObject();
        Assert.True(json["capabilities"]!["tools"]!["listChanged"]!.GetValue<bool>());
        Assert.True(json["capabilities"]!["resources"]!["listChanged"]!.GetValue<bool>());
        Assert.False(json["capabilities"]!["resources"]!["subscribe"]!.GetValue<bool>());
        Assert.Contains(
            json["supportedVersions"]!.AsArray(),
            node => node!.GetValue<string>() == McpSpecKeys.ProtocolVersions.Current);
    }
}

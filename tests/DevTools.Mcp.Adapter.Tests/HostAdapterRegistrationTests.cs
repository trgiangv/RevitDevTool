using DevTools.Mcp.Adapter;
using DevTools.Mcp.Adapter.External;
using DevTools.Mcp.Adapter.Tests.Harness;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Mcp.Adapter.Tests;

[TestClass]
public sealed class HostAdapterRegistrationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void AddMcpHostAdapter_RegistersHostPipeServer()
    {
        var services = new ServiceCollection();
        services.AddMcpHostAdapter();

        Assert.IsTrue(services.Any(descriptor => descriptor.ServiceType == typeof(HostMcpPipeServer)));
    }

    [TestMethod]
    public async Task HostDiscover_AdvertisesListChanged()
    {
        var handler = McpHostTestHarness.CreateHandler(McpHostTestHarness.CreateCatalogStore());
        var response = await handler.HandleAsync(
            McpHostTestHarness.CreateDiscoverRequest(),
            TestContext.CancellationToken);

        var json = response!["result"]!.AsObject();
        Assert.IsTrue(json["capabilities"]!["tools"]!["listChanged"]!.GetValue<bool>());
        Assert.IsTrue(json["capabilities"]!["resources"]!["listChanged"]!.GetValue<bool>());
        Assert.IsFalse(json["capabilities"]!["resources"]!["subscribe"]!.GetValue<bool>());
        Assert.IsTrue(json["supportedVersions"]!.AsArray().Any(
            node => node!.GetValue<string>() == McpSpecKeys.ProtocolVersions.Current));
    }
}

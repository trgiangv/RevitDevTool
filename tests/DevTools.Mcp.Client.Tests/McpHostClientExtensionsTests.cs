using DevTools.Mcp.Client;
using DevTools.Mcp.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevTools.Mcp.Client.Tests;

public sealed class McpHostClientExtensionsTests
{
    [Fact]
    public void AddMcpHostClient_RegistersBrokerScannerAndDiscovery()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMcpHostClient();
        var provider = services.BuildServiceProvider();

        Assert.IsType<McpPipeScanner>(provider.GetRequiredService<IMcpPipeScanner>());
        Assert.IsType<HostBroker>(provider.GetRequiredService<HostBroker>());
        Assert.Same(provider.GetRequiredService<HostBroker>(), provider.GetRequiredService<IHostBroker>());
        Assert.Same(provider.GetRequiredService<HostBroker>(), provider.GetRequiredService<IHostDiscovery>());
    }
}

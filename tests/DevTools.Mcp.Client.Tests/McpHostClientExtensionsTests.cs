using DevTools.Mcp.Client;
using DevTools.Mcp.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class McpHostClientExtensionsTests
{
    [TestMethod]
    public void AddMcpHostClient_RegistersBrokerScannerAndDiscovery()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMcpHostClient();
        var provider = services.BuildServiceProvider();

        Assert.IsInstanceOfType<McpPipeScanner>(provider.GetRequiredService<IMcpPipeScanner>());
        Assert.IsInstanceOfType<HostBroker>(provider.GetRequiredService<HostBroker>());
        Assert.AreSame(provider.GetRequiredService<HostBroker>(), provider.GetRequiredService<IHostBroker>());
        Assert.AreSame(provider.GetRequiredService<HostBroker>(), provider.GetRequiredService<IHostDiscovery>());
    }
}

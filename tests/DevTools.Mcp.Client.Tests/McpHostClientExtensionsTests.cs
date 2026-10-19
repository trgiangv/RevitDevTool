using DevTools.Daemon;
using DevTools.Daemon.Mcp.Processes;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class McpServicesExtensionsTests
{
    [TestMethod]
    public void AddMcp_RegistersHostConnectionsScannerAndEngine()
    {
        using var host = AppHostBuilder.CreateStdioHostForTests();
        var provider = host.Services;

        Assert.IsInstanceOfType<McpPipeScanner>(provider.GetRequiredService<IMcpPipeScanner>());
        Assert.IsInstanceOfType<HostConnections>(provider.GetRequiredService<HostConnections>());
        Assert.IsNotNull(provider.GetRequiredService<IHostConnections>());
    }
}

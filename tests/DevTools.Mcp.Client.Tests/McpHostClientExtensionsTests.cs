using DevTools.Daemon.Composition;
using DevTools.Daemon.Mcp.Processes;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class McpServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddMcp_RegistersProcessSessionsScannerAndEngine()
    {
        using var host = ServerHostBuilder.CreateStdioHostForTests();
        var provider = host.Services;

        Assert.IsInstanceOfType<McpPipeScanner>(provider.GetRequiredService<IMcpPipeScanner>());
        Assert.IsInstanceOfType<ProcessSessions>(provider.GetRequiredService<ProcessSessions>());
        Assert.IsNotNull(provider.GetRequiredService<IProcessSessions>());
    }
}

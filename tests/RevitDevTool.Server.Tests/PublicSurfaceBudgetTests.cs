using DevTools.Daemon.Hosting;
using DevTools.Daemon.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using System.Text;
using System.Text.Json;

namespace RevitDevTool.Server.Tests;

public sealed class PublicSurfaceBudgetTests
{
    [Fact]
    public void BrokerSurface_IsStableAndWithinTokenProxyBudget()
    {
        using var host = DaemonHostBuilder.CreateStdioHost([]);
        var engine = host.Services.GetRequiredService<McpEngine>();
        var protocolTools = engine.LocalTools.Select(tool => tool.ProtocolTool).ToArray();
        var json = JsonSerializer.Serialize(protocolTools, McpJsonUtilities.DefaultOptions);

        Assert.Equal(6, protocolTools.Length);
        Assert.Equal(
            ["devtools_invoke", "devtools_search", "launch_host", "list_machines", "open_model", "read_file_info"],
            protocolTools.Select(tool => tool.Name).Order().ToArray());
        var listMachines = Assert.Single(protocolTools, tool => tool.Name == "list_machines");
        Assert.Contains("x-target-machine", listMachines.Description);
        Assert.True(Encoding.UTF8.GetByteCount(json) <= 16 * 1024, json);
    }
}

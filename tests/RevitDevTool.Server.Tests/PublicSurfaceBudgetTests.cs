using DevTools.Daemon.Hosting;
using DevTools.Daemon.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using System.Text;
using System.Text.Json;

namespace RevitDevTool.Server.Tests;

#pragma warning disable MCPEXP001
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
        var invoke = Assert.Single(protocolTools, tool => tool.Name == "devtools_invoke");
        var hostId = invoke.InputSchema.GetProperty("properties").GetProperty("hostId");
        var hostIdTypes = hostId.GetProperty("type");
        var containsInteger = hostIdTypes.ValueKind == JsonValueKind.String
            ? hostIdTypes.GetString() == "integer"
            : hostIdTypes.EnumerateArray().Any(type => type.GetString() == "integer");
        Assert.True(containsInteger);
        Assert.Contains("process ID", hostId.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        var timeout = invoke.InputSchema.GetProperty("properties").GetProperty("timeoutSeconds");
        Assert.Equal(1, timeout.GetProperty("minimum").GetInt32());
        Assert.Equal(900, timeout.GetProperty("maximum").GetInt32());
        Assert.Equal(300, timeout.GetProperty("default").GetInt32());

        var launch = Assert.Single(protocolTools, tool => tool.Name == "launch_host");
        Assert.Equal(ToolTaskSupport.Optional, launch.Execution?.TaskSupport);
        Assert.Contains("hostApp", launch.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));

        var open = Assert.Single(protocolTools, tool => tool.Name == "open_model");
        Assert.Equal(ToolTaskSupport.Optional, open.Execution?.TaskSupport);
        Assert.Contains("filePath", open.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        var openHostId = open.InputSchema.GetProperty("properties").GetProperty("hostId").GetProperty("type");
        Assert.Contains("integer", openHostId.ValueKind == JsonValueKind.Array
            ? openHostId.EnumerateArray().Select(item => item.GetString())
            : [openHostId.GetString()]);

        var read = Assert.Single(protocolTools, tool => tool.Name == "read_file_info");
        Assert.Contains("filePath", read.InputSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal("object", Assert.Single(protocolTools, tool => tool.Name == "list_machines")
            .InputSchema.GetProperty("type").GetString());

        var root = FindRepositoryRoot();
        foreach (var fileName in new[] { "LaunchHostTool.cs", "OpenModelTool.cs", "ReadFileInfoTool.cs", "ListMachinesTool.cs" })
        {
            var source = File.ReadAllText(Path.Combine(root, "source", "DevTools.Daemon", "Mcp", "Tools", fileName));
            Assert.DoesNotContain(": McpServerTool", source, StringComparison.Ordinal);
            Assert.DoesNotContain("InputSchema =", source, StringComparison.Ordinal);
            Assert.Contains("[McpServerTool(", source, StringComparison.Ordinal);
        }
        Assert.True(Encoding.UTF8.GetByteCount(json) <= 16 * 1024, json);
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "RevitDevTool.slnx")))
                return current.FullName;
        throw new DirectoryNotFoundException("RevitDevTool.slnx was not found.");
    }
}
#pragma warning restore MCPEXP001

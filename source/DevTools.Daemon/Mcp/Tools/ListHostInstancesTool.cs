using System.Text.Json;
using DevTools.McpParser.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class ListHostInstancesTool(InstanceManager instanceManager) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "list_host_instances",
        Description =
            "List connected and discovered host instances. " +
            "Returns hostApp, processId, and version for each instance.",
        InputSchema = JsonSerializer.SerializeToElement(new { type = JsonSchemaTypeNames.Object, properties = new { } })
    };

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var instances = instanceManager.GetInstances();
        var discoveredPipes = instanceManager.GetDiscoveredPipeNames();

        var result = new
        {
            connectedInstances = instances.Select(i => new
            {
                hostApp = i.HostApp ?? HostAppExtensions.FromPipeName(
                    instanceManager.GetPipeNameByProcessId(i.ProcessId) ?? "")?.ToString(),
                processId = i.ProcessId,
                versionNumber = i.VersionNumber
            }),
            discoveredPipes = discoveredPipes
                .Select(p => new
                {
                    pipeName = p,
                    hostApp = HostAppExtensions.FromPipeName(p)?.ToString()
                })
                .ToArray(),
            totalConnected = instances.Count,
            totalDiscovered = discoveredPipes.Count
        };

        return ValueTask.FromResult(new CallToolResult
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(result, ToolHelpers.IndentedJsonOptions) }]
        });
    }
}

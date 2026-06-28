using System.Text.Json;
using DevTools.Daemon.Contracts;
using DevTools.Mcp.Schema;
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
        InputSchema = McpSchemaBuilder.EmptyObject()
    };

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var instances = instanceManager.GetInstances();
        var discoveredPipes = instanceManager.GetDiscoveredPipeNames();

        var result = new ListInstancesResult(
            instances.Select(i => new ConnectedInstanceEntry(
                HostAppExtensions.ParseHostApp(i.HostApp) ?? HostAppExtensions.FromPipeName(
                    instanceManager.GetPipeNameByProcessId(i.ProcessId) ?? ""),
                i.ProcessId,
                i.VersionNumber)).ToArray(),
            discoveredPipes
                .Select(p => new DiscoveredPipeEntry(
                    p,
                    HostAppExtensions.FromPipeName(p)))
                .ToArray(),
            instances.Count,
            discoveredPipes.Count);

        return ValueTask.FromResult(new CallToolResult
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(result, ToolHelpers.IndentedJsonOptions) }]
        });
    }
}

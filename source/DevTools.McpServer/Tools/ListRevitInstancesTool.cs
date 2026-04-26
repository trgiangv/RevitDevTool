using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.McpServer.Tools;

public sealed class ListRevitInstancesTool(InstanceManager instanceManager) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "list_revit_instances",
        Description = "List discovered and connected Revit instances managed by MCPServer. Use this first to choose a target `revitInstanceId` for tools that route into running Revit sessions.",
        InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { } })
    };

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var instances = instanceManager.GetInstances();
        var discoveredPipes = InstanceManager.DiscoverRevitPipes();

        var result = new
        {
            connectedInstances = instances.Select(i => new
            {
                processId = i.ProcessId,
                versionNumber = i.VersionNumber,
                documentTitle = i.DocumentTitle ?? "(no document)",
                documentPath = i.DocumentPath
            }),
            discoveredPipes = discoveredPipes.ToArray(),
            totalConnected = instances.Count,
            totalDiscovered = discoveredPipes.Count
        };

        return ValueTask.FromResult(new CallToolResult
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) }]
        });
    }
}

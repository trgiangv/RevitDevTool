using System.Text.Json;
using DevTools.Mcp.Routing.Catalog;
using DevTools.Mcp.Schema;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class ListDynamicResources(DynamicResourceCatalog catalog) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "list_dynamic_resources",
        Description =
            "List resources currently registered by each connected host instance. " +
            "Use hostInstanceId with read_dynamic_resource when a resource is available on multiple instances.",
        InputSchema = McpSchemaBuilder.EmptyObject()
    };

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var registrations = catalog.List();
        var grouped = registrations
            .GroupBy(r => r.Uri, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                uri = g.Key,
                name = g.First().Name,
                description = g.First().Description,
                mimeType = g.First().MimeType,
                instances = g.Select(r => new
                {
                    hostInstanceId = r.Instance.ProcessId,
                    hostApp = r.Instance.HostApp,
                    version = r.Instance.VersionNumber
                }).ToArray()
            })
            .ToArray();

        return ValueTask.FromResult(new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = JsonSerializer.Serialize(
                        new { resources = grouped, count = grouped.Length },
                        ToolHelpers.IndentedJsonOptions)
                }
            ]
        });
    }
}

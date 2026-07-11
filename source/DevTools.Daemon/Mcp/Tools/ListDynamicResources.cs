using System.Text.Json;
using DevTools.Mcp.Schema;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class ListDynamicResources(McpServerResourceCollection resourceCollection) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "list_dynamic_resources",
        Description =
            "List resources currently exposed by connected host instances. " +
            "Returns URIs, names, and descriptions for each available resource.",
        InputSchema = McpSchemaBuilder.EmptyObject()
    };

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var items = resourceCollection
            .Select(r => new
            {
                uri = r.ProtocolResource?.Uri ?? r.ProtocolResourceTemplate.UriTemplate,
                name = r.ProtocolResource?.Name ?? r.ProtocolResourceTemplate.Name,
                description = r.ProtocolResource?.Description ?? r.ProtocolResourceTemplate.Description,
                mimeType = r.ProtocolResource?.MimeType ?? r.ProtocolResourceTemplate.MimeType
            })
            .ToArray();

        return ValueTask.FromResult(new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = JsonSerializer.Serialize(
                        new { resources = items, count = items.Length },
                        ToolHelpers.IndentedJsonOptions)
                }
            ]
        });
    }
}

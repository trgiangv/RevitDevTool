using System.Text.Json;
using DevTools.McpParser.Models;
using DevTools.McpServer.Catalog;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.McpServer.Tools;

public sealed class ListDynamicTools(DynamicToolCatalog catalog) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "list_dynamic_tools",
        Description =
            "List tools currently registered by each connected host instance. " +
            "Use hostInstanceId with call_dynamic_tool when a tool is available on multiple instances.",
        InputSchema = JsonSerializer.SerializeToElement(new { type = JsonSchemaTypeNames.Object, properties = new { } })
    };

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Result());

    private CallToolResult Result() =>
        new()
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = JsonSerializer.Serialize(
                        catalog.Build(),
                        ToolHelpers.IndentedJsonOptions)
                }
            ]
        };
}

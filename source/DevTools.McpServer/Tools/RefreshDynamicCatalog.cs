using System.Text.Json;
using DevTools.McpParser.Models;
using DevTools.McpServer.Catalog;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.McpServer.Tools;

public sealed class RefreshDynamicCatalog(
    DynamicToolCatalog catalog,
    Func<CancellationToken, Task> refresh) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "refresh_dynamic_catalog",
        Description =
            "Query all connected host instances again and return the refreshed dynamic tool catalog.",
        InputSchema = JsonSerializer.SerializeToElement(new { type = JsonSchemaTypeNames.Object, properties = new { } })
    };

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        await refresh(cancellationToken).ConfigureAwait(false);
        return new CallToolResult
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
}

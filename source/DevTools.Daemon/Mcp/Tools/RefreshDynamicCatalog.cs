using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class RefreshDynamicCatalog(DynamicToolCatalog catalog) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "refresh_dynamic_catalog",
        Description =
            "Query all connected host instances again and return the refreshed dynamic tool catalog.",
        InputSchema = JsonSerializer.SerializeToElement(new { type = JsonSchemaTypeNames.Object, properties = new { } })
    };

    public override IReadOnlyList<object> Metadata => [];

    /// <summary>
    /// Set by the hosting layer after CatalogService is created.
    /// </summary>
    public Func<CancellationToken, Task>? RefreshDelegate { get; set; }

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        if (RefreshDelegate is { } refresh)
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

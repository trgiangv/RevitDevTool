using System.Text.Json;
using DevTools.Mcp.Routing.Catalog;
using DevTools.Mcp.Schema;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class RefreshDynamicCatalog(
    DynamicToolCatalog catalog,
    McpServerResourceCollection resourceCollection,
    McpServerPrimitiveCollection<McpServerPrompt> promptCollection) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "refresh_dynamic_catalog",
        Description =
            "Re-query all connected host instances and rebuild the full catalog (tools, resources, prompts). Returns the refreshed tool list.",
        InputSchema = McpSchemaBuilder.EmptyObject()
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

        var summary = new
        {
            tools = catalog.Build(),
            resources = resourceCollection.Count(),
            prompts = promptCollection.Count()
        };

        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = JsonSerializer.Serialize(summary, ToolHelpers.IndentedJsonOptions)
                }
            ]
        };
    }
}

using System.Text.Json;
using DevTools.Mcp.Routing.Catalog;
using DevTools.Mcp.Schema;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class ListDynamicPrompts(DynamicPromptCatalog catalog) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "list_dynamic_prompts",
        Description =
            "List prompts currently registered by each connected host instance. " +
            "Use hostInstanceId with get_dynamic_prompt when a prompt is available on multiple instances.",
        InputSchema = McpSchemaBuilder.EmptyObject()
    };

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var registrations = catalog.List();
        var grouped = registrations
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                name = g.Key,
                description = g.First().Description,
                arguments = g.First().ProtocolPrompt.Arguments?.Select(a => new
                {
                    name = a.Name,
                    description = a.Description,
                    required = a.Required
                }),
                instances = g.Select(p => new
                {
                    hostInstanceId = p.Instance.ProcessId,
                    hostApp = p.Instance.HostApp,
                    version = p.Instance.VersionNumber
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
                        new { prompts = grouped, count = grouped.Length },
                        ToolHelpers.IndentedJsonOptions)
                }
            ]
        });
    }
}

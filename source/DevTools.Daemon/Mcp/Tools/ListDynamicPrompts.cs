using System.Text.Json;
using DevTools.Mcp.Schema;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class ListDynamicPrompts(McpServerPrimitiveCollection<McpServerPrompt> promptCollection) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "list_dynamic_prompts",
        Description =
            "List prompts currently exposed by connected host instances. " +
            "Returns prompt names, descriptions, and their arguments.",
        InputSchema = McpSchemaBuilder.EmptyObject()
    };

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var items = promptCollection
            .Select(p => new
            {
                name = p.ProtocolPrompt.Name,
                description = p.ProtocolPrompt.Description,
                arguments = p.ProtocolPrompt.Arguments?.Select(a => new
                {
                    name = a.Name,
                    description = a.Description,
                    required = a.Required
                })
            })
            .ToArray();

        return ValueTask.FromResult(new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = JsonSerializer.Serialize(
                        new { prompts = items, count = items.Length },
                        ToolHelpers.IndentedJsonOptions)
                }
            ]
        });
    }
}

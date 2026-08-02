using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpToolsetDemo;

/// <summary>
/// SPIKE (2025 ILRepack + TypeForwardedTo): returns native <see cref="CallToolResult"/>
/// so host can map via <see cref="ToolsetResultSerializer"/> without foreign-type STJ.
/// </summary>
[McpServerToolType]
public static class McpForwarderSpikeTool
{
    [McpServerTool(
        Name = "test_forwarder_calltoolresult",
        Title = "Test Forwarder CallToolResult",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Spike: native CallToolResult return to validate MCP type forwarders after ILRepack.")]
    public static CallToolResult TestForwarderCallToolResult()
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = "forwarder-spike-ok" }],
            StructuredContent = JsonSerializer.SerializeToElement(new { spike = true }),
        };
    }
}

using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests.Harness;

internal static class McpToolInvoke
{
    public static string Text(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().Single().Text;
}

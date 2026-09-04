using DevTools.Mcp.Core.Results;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests.Harness;

internal static class McpToolInvoke
{
    public static string Text(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().Single().Text;

    public static string Text(McpInvocationResponse result) =>
        result.Content.OfType<McpTextContent>().Single().Text;
}

using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Mcp.Server.Tests.Harness;

internal static class McpToolInvoke
{
    public static async Task<CallToolResult> Invoke(McpServerTool tool, string name, object args)
    {
        var argumentMap = JsonSerializer.SerializeToElement(args).EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value);

        var server = new Mock<McpServer>();
        server.Setup(s => s.IsMrtrSupported).Returns(true);

        return await tool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                server.Object,
                new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
                new CallToolRequestParams
                {
                    Name = name,
                    Arguments = argumentMap,
                }),
            CancellationToken.None);
    }

    public static string Text(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().Single().Text;
}

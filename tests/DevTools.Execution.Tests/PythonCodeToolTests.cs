using System.Text.Json;
using DevTools.Execution.External.Mcp.BuiltIn;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Execution.Tests;

public sealed class PythonCodeToolTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_EmptyOrWhitespaceCode_ReturnsErrorBeforeInitialization(string code)
    {
        var tool = new PythonCodeTool(null!, null!, null!);
        var result = await InvokeToolAsync(tool, new { code });

        Assert.True(result.IsError);
        Assert.Contains("Code parameter must not be empty.", Text(result));
    }

    private static async Task<CallToolResult> InvokeToolAsync(PythonCodeTool tool, object args)
    {
        var argumentMap = JsonSerializer.SerializeToElement(args).EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value);

        return await tool.ServerTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                Mock.Of<McpServer>(),
                new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
                new CallToolRequestParams
                {
                    Name = tool.Name,
                    Arguments = argumentMap,
                }),
            TestContext.Current.CancellationToken);
    }

    private static string Text(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().Single().Text;
}

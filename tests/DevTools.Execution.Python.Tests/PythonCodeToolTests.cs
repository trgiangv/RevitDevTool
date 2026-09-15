using System.Text.Json;
using DevTools.Execution.External.Mcp.BuiltIn;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PythonCodeToolTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task Execute_EmptyOrWhitespaceCode_ReturnsErrorBeforeInitialization(string code)
    {
        var tool = new PythonCodeTool(null!, null!);
        var result = await InvokeToolAsync(tool, new { code });

        Assert.IsTrue(result.IsError);
        Assert.Contains("Code parameter must not be empty.", Text(result));
    }

    private async Task<CallToolResult> InvokeToolAsync(PythonCodeTool tool, object args)
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
            TestContext.CancellationToken);
    }

    private static string Text(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().Single().Text;
}

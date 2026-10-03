using System.Text.Json;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Hosting;
using DevTools.Mcp.Core.Protocol;
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
        var tool = new PythonCodeTool(null!, null!, Host(HostApp.Revit));
        var result = await InvokeToolAsync(tool, new { code });

        Assert.IsTrue(result.IsError);
        Assert.Contains("Code parameter must not be empty.", Text(result));
    }

    [TestMethod]
    public void RevitDescription_RequiresPythonScriptAndCSharpBoundary()
    {
        var description = Advertise(HostApp.Revit);

        Assert.Contains(McpSpecKeys.Resource.RevitPythonCheatsheet, description, StringComparison.Ordinal);
        Assert.Contains("def run()", description, StringComparison.Ordinal);
        Assert.Contains(McpSpecKeys.Tool.ExecuteCSharp, description, StringComparison.Ordinal);
        Assert.Contains("does not run IExternalCommand", description, StringComparison.Ordinal);
        Assert.DoesNotContain(McpSpecKeys.Resource.AcadPythonCheatsheet, description, StringComparison.Ordinal);
        Assert.DoesNotContain("[CommandMethod]", description, StringComparison.Ordinal);
    }

    [TestMethod]
    public void AutoCadDescription_RequiresPythonScriptAndCSharpBoundary()
    {
        var description = Advertise(HostApp.AutoCad);

        Assert.Contains(McpSpecKeys.Resource.AcadPythonCheatsheet, description, StringComparison.Ordinal);
        Assert.Contains("def run()", description, StringComparison.Ordinal);
        Assert.Contains(McpSpecKeys.Tool.ExecuteCSharp, description, StringComparison.Ordinal);
        Assert.Contains("does not run [CommandMethod]", description, StringComparison.Ordinal);
        Assert.DoesNotContain(McpSpecKeys.Resource.RevitPythonCheatsheet, description, StringComparison.Ordinal);
        Assert.DoesNotContain("IExternalCommand", description, StringComparison.Ordinal);
    }

    private static string Advertise(HostApp host) =>
        new PythonCodeTool(null!, null!, Host(host)).ServerTool.ProtocolTool.Description ?? "";

    private static IHostAppInfo Host(HostApp host)
    {
        var info = new Mock<IHostAppInfo>();
        info.Setup(item => item.Host).Returns(host);
        return info.Object;
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

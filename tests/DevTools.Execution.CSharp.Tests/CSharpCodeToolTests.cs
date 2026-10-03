using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using DevTools.Hosting;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class CSharpCodeToolTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task Execute_EmptyOrWhitespaceCode_ReturnsCompilationErrorWithoutRunningHost(string code)
    {
        var bridge = new Mock<ICompiledScriptBridge>();
        var hostContext = new Mock<IHostContextExecutor>();
        var commandRunner = new Mock<ICommandRunner>();
        var compiler = new CSharpCompiler(
            NullLogger<CSharpCompiler>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));

        var tool = new CSharpCodeTool(bridge.Object, compiler, hostContext.Object, commandRunner.Object, Host(HostApp.Revit));
        var result = await InvokeToolAsync(tool, new { code }, TestContext.CancellationToken);

        Assert.IsTrue(result.IsError);
        Assert.Contains(McpSpecKeys.Result.Compilation, Text(result));
        hostContext.Verify(
            h => h.ExecuteAsync(It.IsAny<Func<Execution.Models.ExecutionResult>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        commandRunner.Verify(r => r.RunCompiledCommand(It.IsAny<object>()), Times.Never);
    }

    [TestMethod]
    public async Task Execute_InvalidCode_ReturnsCompilationErrorWithoutRunningHost()
    {
        var bridge = new Mock<ICompiledScriptBridge>();
        bridge.Setup(b => b.RewriteHostReference(It.IsAny<string>())).Returns((string reference) => reference);

        var hostContext = new Mock<IHostContextExecutor>();
        var commandRunner = new Mock<ICommandRunner>();
        var compiler = new CSharpCompiler(
            NullLogger<CSharpCompiler>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));

        var tool = new CSharpCodeTool(bridge.Object, compiler, hostContext.Object, commandRunner.Object, Host(HostApp.Revit));
        var result = await InvokeToolAsync(tool, new { code = "public class {{" }, TestContext.CancellationToken);

        Assert.IsTrue(result.IsError);
        Assert.Contains(McpSpecKeys.Result.Compilation, Text(result));
        hostContext.Verify(
            h => h.ExecuteAsync(It.IsAny<Func<Execution.Models.ExecutionResult>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        commandRunner.Verify(r => r.RunCompiledCommand(It.IsAny<object>()), Times.Never);
    }

    [TestMethod]
    public void RevitDescription_RequiresIExternalCommandAndRevitCheatsheet()
    {
        var description = Advertise(HostApp.Revit);

        Assert.Contains("IExternalCommand", description, StringComparison.Ordinal);
        Assert.Contains(McpSpecKeys.Resource.RevitCSharpCheatsheet, description, StringComparison.Ordinal);
        Assert.Contains("ExternalCommandData commandData", description, StringComparison.Ordinal);
        Assert.Contains(McpSpecKeys.Tool.ExecutePython, description, StringComparison.Ordinal);
        Assert.DoesNotContain("TransactionMode.ReadOnly", description, StringComparison.Ordinal);
        Assert.DoesNotContain(McpSpecKeys.Resource.AcadCSharpCheatsheet, description, StringComparison.Ordinal);
    }

    [TestMethod]
    public void AutoCadDescription_RequiresCommandMethodAndAcadCheatsheet()
    {
        var description = Advertise(HostApp.AutoCad);

        Assert.Contains("[CommandMethod]", description, StringComparison.Ordinal);
        Assert.Contains(McpSpecKeys.Resource.AcadCSharpCheatsheet, description, StringComparison.Ordinal);
        Assert.Contains("CommandFlags.Session", description, StringComparison.Ordinal);
        Assert.Contains(McpSpecKeys.Tool.ExecutePython, description, StringComparison.Ordinal);
        Assert.DoesNotContain(McpSpecKeys.Resource.RevitCSharpCheatsheet, description, StringComparison.Ordinal);
        Assert.DoesNotContain("IExternalCommand", description, StringComparison.Ordinal);
    }

    private static string Advertise(HostApp host)
    {
        var tool = new CSharpCodeTool(
            Mock.Of<ICompiledScriptBridge>(),
            new CSharpCompiler(NullLogger<CSharpCompiler>.Instance, new NugetManager(NullLogger<NugetManager>.Instance)),
            Mock.Of<IHostContextExecutor>(),
            Mock.Of<ICommandRunner>(),
            Host(host));

        return tool.ServerTool.ProtocolTool.Description ?? "";
    }

    private static IHostAppInfo Host(HostApp host)
    {
        var info = new Mock<IHostAppInfo>();
        info.Setup(item => item.Host).Returns(host);
        return info.Object;
    }

    private static async Task<CallToolResult> InvokeToolAsync(CSharpCodeTool tool, object args, CancellationToken cancellationToken)
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
            cancellationToken);
    }

    private static string Text(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().Single().Text;
}

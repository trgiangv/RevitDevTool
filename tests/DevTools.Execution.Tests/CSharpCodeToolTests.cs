using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Execution.Tests;

public sealed class CSharpCodeToolTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_EmptyOrWhitespaceCode_ReturnsCompilationErrorWithoutRunningHost(string code)
    {
        var bridge = new Mock<ICompiledScriptBridge>();
        var hostContext = new Mock<IHostContextExecutor>();
        var commandRunner = new Mock<ICommandRunner>();
        var compiler = new CSharpCompiler(
            NullLogger<CSharpCompiler>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));

        var tool = new CSharpCodeTool(bridge.Object, compiler, hostContext.Object, commandRunner.Object);
        var result = await InvokeToolAsync(tool, new { code });

        Assert.True(result.IsError);
        Assert.Contains("[COMPILATION ERROR]", Text(result));
        hostContext.Verify(
            h => h.ExecuteAsync(It.IsAny<Func<Execution.Models.ExecutionResult>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        commandRunner.Verify(r => r.RunCompiledCommand(It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task Execute_InvalidCode_ReturnsCompilationErrorWithoutRunningHost()
    {
        var bridge = new Mock<ICompiledScriptBridge>();
        bridge.Setup(b => b.GetHostReferencePattern()).Returns((string?)null);
        bridge.Setup(b => b.GetHostReferenceReplacement()).Returns(string.Empty);

        var hostContext = new Mock<IHostContextExecutor>();
        var commandRunner = new Mock<ICommandRunner>();
        var compiler = new CSharpCompiler(
            NullLogger<CSharpCompiler>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));

        var tool = new CSharpCodeTool(bridge.Object, compiler, hostContext.Object, commandRunner.Object);
        var result = await InvokeToolAsync(tool, new { code = "public class {{" });

        Assert.True(result.IsError);
        Assert.Contains("[COMPILATION ERROR]", Text(result));
        hostContext.Verify(
            h => h.ExecuteAsync(It.IsAny<Func<Execution.Models.ExecutionResult>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        commandRunner.Verify(r => r.RunCompiledCommand(It.IsAny<object>()), Times.Never);
    }

    private static async Task<CallToolResult> InvokeToolAsync(CSharpCodeTool tool, object args)
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

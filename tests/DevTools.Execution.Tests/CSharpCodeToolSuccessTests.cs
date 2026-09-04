using System.Text.Json;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;

namespace DevTools.Execution.Tests;

public sealed class CSharpCodeToolSuccessTests
{
    [Fact]
    public async Task Execute_ValidCode_CompilesAndRunsCommand()
    {
        const string code = """
                              public sealed class ScriptCommand
                              {
                                  public static int M() => 42;
                              }
                              """;

        var bridge = ExecutionTestHelpers.CreateScriptBridge();
        var hostContext = ExecutionTestHelpers.MockHostContext();
        var commandRunner = new Mock<ICommandRunner>();
        commandRunner
            .Setup(r => r.RunCompiledCommand(It.IsAny<object>()))
            .Returns(ExecutionResult.Succeeded("ok"));
        var compiler = new CSharpCompiler(
            NullLogger<CSharpCompiler>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));
        var tool = new CSharpCodeTool(bridge, compiler, hostContext.Object, commandRunner.Object);

        var result = await InvokeToolAsync(tool, new { code });

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("ok", Text(result), StringComparison.Ordinal);
        commandRunner.Verify(r => r.RunCompiledCommand(It.IsAny<object>()), Times.Once);
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

using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.Backends;
using DevTools.Mcp.Discovery;
using DevTools.Mcp.Isolation;
using DevTools.Mcp.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class DotnetSourceAsyncTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task InvokeToolAsync_CompletedAsyncTool_ReturnsResult()
    {
        var backend = CreateBackend();
        var tool = new RegisteredTool
        {
            Id = "async-echo",
            Descriptor = new Tool { Name = "async_echo" },
            Binding = PrimitiveBinding.Create(
                ExecutionMode.Dotnet,
                typeof(ExecutionDotnetAsyncToolStubs).Assembly.Location,
                typeof(ExecutionDotnetAsyncToolStubs).FullName!,
                nameof(ExecutionDotnetAsyncToolStubs.EchoAsync),
                "",
                ""),
        };

        var result = await backend.InvokeToolAsync(
            tool,
            new CallToolRequestParams
            {
                Name = "async_echo",
                Arguments = new Dictionary<string, System.Text.Json.JsonElement>
                {
                    ["message"] = System.Text.Json.JsonSerializer.SerializeToElement("hello-async"),
                },
            },
            ExecutionTestHelpers.InlineHostContext(),
            TestContext.CancellationToken);

        Assert.AreNotEqual(true, result.IsError);
        Assert.AreEqual("hello-async", Assert.IsInstanceOfType<TextContentBlock>(result.Content.Single()).Text);
    }

    private static DotnetSource CreateBackend()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        return new DotnetSource(
            provider,
            new DotnetMethodResolver(
                new McpToolsetContextManager(NullLogger<McpToolsetContextManager>.Instance),
                NullLogger<DotnetMethodResolver>.Instance));
    }
}

[McpServerToolType]
internal static class ExecutionDotnetAsyncToolStubs
{
    [McpServerTool(Name = "async_echo")]
    public static async Task<string> EchoAsync(string message)
    {
        await Task.CompletedTask;
        return message;
    }
}

using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.Backends;
using DevTools.Execution.Services;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Catalog.Isolation;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.Tests;

public sealed class DotnetMcpToolBackendInvokeTests
{
    [Fact]
    public async Task InvokeToolAsync_ResolvesStaticTool_ReturnsSuccess()
    {
        var backend = CreateBackend();
        var tool = new McpRegisteredTool
        {
            Id = "execution-echo",
            Descriptor = new Tool { Name = "execution_echo" },
            Binding = McpPrimitiveBinding.Create(
                ExecutionMode.Dotnet,
                typeof(ExecutionDotnetMcpStubs).Assembly.Location,
                typeof(ExecutionDotnetMcpStubs).FullName!,
                nameof(ExecutionDotnetMcpStubs.Echo)),
        };

        var result = await backend.InvokeToolAsync(
            tool,
            new CallToolRequestParams
            {
                Name = "execution_echo",
                Arguments = new Dictionary<string, System.Text.Json.JsonElement>
                {
                    ["message"] = System.Text.Json.JsonSerializer.SerializeToElement("hello"),
                },
            },
            ExecutionTestHelpers.InlineHostContext(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task InvokeToolAsync_UnknownTool_ReturnsFailure()
    {
        var backend = CreateBackend();
        var tool = new McpRegisteredTool
        {
            Id = "missing",
            Descriptor = new Tool { Name = "missing" },
            Binding = McpPrimitiveBinding.Create(ExecutionMode.Dotnet, string.Empty, "Missing", "Run"),
        };

        var result = await backend.InvokeToolAsync(
            tool,
            new CallToolRequestParams { Name = "missing" },
            ExecutionTestHelpers.InlineHostContext(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task InvokeToolAsync_SecondCall_UsesCachedTool()
    {
        var backend = CreateBackend();
        var tool = new McpRegisteredTool
        {
            Id = "execution-echo-cache",
            Descriptor = new Tool { Name = "execution_echo" },
            Binding = McpPrimitiveBinding.Create(
                ExecutionMode.Dotnet,
                typeof(ExecutionDotnetMcpStubs).Assembly.Location,
                typeof(ExecutionDotnetMcpStubs).FullName!,
                nameof(ExecutionDotnetMcpStubs.Echo)),
        };

        var request = new CallToolRequestParams
        {
            Name = "execution_echo",
            Arguments = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["message"] = System.Text.Json.JsonSerializer.SerializeToElement("one"),
            },
        };

        var first = await backend.InvokeToolAsync(tool, request, ExecutionTestHelpers.InlineHostContext(), TestContext.Current.CancellationToken);
        var second = await backend.InvokeToolAsync(tool, request, ExecutionTestHelpers.InlineHostContext(), TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        backend.ClearCaches();
    }

    [Fact]
    public async Task InvokeToolAsync_InstanceTool_ReturnsSuccess()
    {
        var backend = CreateBackend();
        var tool = new McpRegisteredTool
        {
            Id = "execution-instance",
            Descriptor = new Tool { Name = "execution_instance" },
            Binding = McpPrimitiveBinding.Create(
                ExecutionMode.Dotnet,
                typeof(ExecutionDotnetMcpInstanceStubs).Assembly.Location,
                typeof(ExecutionDotnetMcpInstanceStubs).FullName!,
                nameof(ExecutionDotnetMcpInstanceStubs.InstanceEcho)),
        };

        var result = await backend.InvokeToolAsync(
            tool,
            new CallToolRequestParams
            {
                Name = "execution_instance",
                Arguments = new Dictionary<string, System.Text.Json.JsonElement>
                {
                    ["message"] = System.Text.Json.JsonSerializer.SerializeToElement("instance"),
                },
            },
            ExecutionTestHelpers.InlineHostContext(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    private static DotnetMcpToolBackend CreateBackend()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        return new DotnetMcpToolBackend(
            provider,
            new DotnetMethodResolver(
                new McpToolsetContextManager(NullLogger<McpToolsetContextManager>.Instance),
                NullLogger<DotnetMethodResolver>.Instance));
    }
}

[McpServerToolType]
internal sealed class ExecutionDotnetMcpInstanceStubs
{
    [McpServerTool(Name = "execution_instance")]
    public CallToolResult InstanceEcho(string message = "hello") =>
        new() { Content = [new TextContentBlock { Text = message }] };
}

[McpServerToolType]
internal static class ExecutionDotnetMcpStubs
{
    [McpServerTool(Name = "execution_echo")]
    public static CallToolResult Echo(string message = "hello") =>
        new() { Content = [new TextContentBlock { Text = message }] };
}

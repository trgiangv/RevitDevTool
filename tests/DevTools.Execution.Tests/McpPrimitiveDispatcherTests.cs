using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.Dispatchers;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Results;
using DevTools.Telemetry;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Moq;

namespace DevTools.Execution.Tests;

public sealed class McpPrimitiveDispatcherTests
{
    [Fact]
    public async Task DispatchToolAsync_UnsupportedSourceKind_ReturnsExecutionFailed()
    {
        var dispatcher = CreateDispatcher(CreateBackend(ExecutionMode.Python).Object);
        var tool = CreateTool(ExecutionMode.Dotnet, "unsupported_tool");

        var result = await dispatcher.DispatchToolAsync(
            tool,
            new CallToolRequestParams { Name = tool.Descriptor.Name },
            Mock.Of<IHostContextExecutor>(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(DevTools.Mcp.Core.Results.McpErrorCode.ExecutionFailed, result.Error!.Code);
        Assert.Contains("Unsupported MCP tool source", result.Error.Message);
    }

    [Fact]
    public async Task DispatchToolAsync_MatchingBackend_ReturnsSuccess()
    {
        var expected = new McpInvocationResponse
        {
            Content = [new McpTextContent("ok")],
        };
        var backend = CreateBackend(ExecutionMode.Python);
        backend
            .Setup(b => b.InvokeToolAsync(
                It.IsAny<McpRegisteredTool>(),
                It.IsAny<CallToolRequestParams>(),
                It.IsAny<IHostContextExecutor>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(McpResult<McpInvocationResponse>.Success(expected));

        var dispatcher = CreateDispatcher(backend.Object);
        var tool = CreateTool(ExecutionMode.Python, "python_tool");

        var result = await dispatcher.DispatchToolAsync(
            tool,
            new CallToolRequestParams { Name = tool.Descriptor.Name },
            Mock.Of<IHostContextExecutor>(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", Assert.IsType<McpTextContent>(result.Value!.Content.Single()).Text);
        backend.Verify(b => b.InvokeToolAsync(
            tool,
            It.IsAny<CallToolRequestParams>(),
            It.IsAny<IHostContextExecutor>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchToolAsync_BackendThrows_ReturnsFailureWithMessage()
    {
        var backend = CreateBackend(ExecutionMode.Python);
        backend
            .Setup(b => b.InvokeToolAsync(
                It.IsAny<McpRegisteredTool>(),
                It.IsAny<CallToolRequestParams>(),
                It.IsAny<IHostContextExecutor>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("backend failed"));

        var dispatcher = CreateDispatcher(backend.Object);
        var tool = CreateTool(ExecutionMode.Python, "python_tool");

        var result = await dispatcher.DispatchToolAsync(
            tool,
            new CallToolRequestParams { Name = tool.Descriptor.Name },
            Mock.Of<IHostContextExecutor>(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(DevTools.Mcp.Core.Results.McpErrorCode.ExecutionFailed, result.Error!.Code);
        Assert.Equal("backend failed", result.Error.Message);
    }

    [Fact]
    public void ReadResource_UnsupportedSource_Throws()
    {
        var dispatcher = CreateDispatcher(CreateBackend(ExecutionMode.Python).Object);
        var resource = CreateResource(ExecutionMode.Dotnet, "unsupported_resource");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            dispatcher.ReadResource(resource, "file:///x", TestContext.Current.CancellationToken));

        Assert.Contains("Unsupported MCP resource source", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadResource_MatchingBackend_ReturnsResult()
    {
        var expected = new ReadResourceResult { Contents = [] };
        var backend = CreateBackend(ExecutionMode.Python);
        backend
            .Setup(b => b.ReadResource(
                It.IsAny<McpRegisteredResource>(),
                "file:///ok",
                It.IsAny<CancellationToken>()))
            .Returns(expected);

        var dispatcher = CreateDispatcher(backend.Object);
        var resource = CreateResource(ExecutionMode.Python, "python_resource");

        var result = dispatcher.ReadResource(resource, "file:///ok", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        backend.Verify(b => b.ReadResource(resource, "file:///ok", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ClearCaches_InvokesAllBackends()
    {
        var first = CreateBackend(ExecutionMode.Python);
        var second = CreateBackend(ExecutionMode.CSharp);
        var dispatcher = CreateDispatcher(first.Object, second.Object);

        dispatcher.ClearCaches();

        first.Verify(b => b.ClearCaches(), Times.Once);
        second.Verify(b => b.ClearCaches(), Times.Once);
    }

    [Fact]
    public async Task DispatchToolAsync_InputRequiredException_MapsToSuccessResponse()
    {
        var backend = CreateBackend(ExecutionMode.Python);
        backend
            .Setup(b => b.InvokeToolAsync(
                It.IsAny<McpRegisteredTool>(),
                It.IsAny<CallToolRequestParams>(),
                It.IsAny<IHostContextExecutor>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InputRequiredException(requestState: "round1"));

        var dispatcher = CreateDispatcher(backend.Object);
        var tool = CreateTool(ExecutionMode.Python, "python_tool");

        var result = await dispatcher.DispatchToolAsync(
            tool,
            new CallToolRequestParams { Name = tool.Descriptor.Name },
            Mock.Of<IHostContextExecutor>(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("round1", result.Value!.InputRequired!.RequestState);
    }

    private static McpPrimitiveDispatcher CreateDispatcher(params IMcpPrimitiveBackend[] backends)
    {
        var telemetry = new Mock<ITelemetry>();
        return new McpPrimitiveDispatcher(backends, telemetry.Object);
    }

    private static Mock<IMcpPrimitiveBackend> CreateBackend(ExecutionMode sourceKind)
    {
        var backend = new Mock<IMcpPrimitiveBackend>();
        backend.SetupGet(b => b.SourceKind).Returns(sourceKind);
        return backend;
    }

    private static McpRegisteredTool CreateTool(ExecutionMode sourceKind, string name) => new()
    {
        Id = name,
        Descriptor = new Tool
        {
            Name = name,
            Description = $"{name} description",
            InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }),
        },
        Binding = McpPrimitiveBinding.Create(sourceKind, "stub.py", "Stub", name),
    };

    private static McpRegisteredResource CreateResource(ExecutionMode sourceKind, string name) => new()
    {
        Id = name,
        Descriptor = new Resource
        {
            Name = name,
            Uri = "file:///stub",
            MimeType = "text/plain",
        },
        Binding = McpPrimitiveBinding.Create(sourceKind, "stub.py", "Stub", name),
    };
}

using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.Backends;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.External.Mcp.Connections;
using DevTools.Execution.Interfaces;
using DevTools.Mcp.Catalog;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Catalog.Isolation;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class McpCoverageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void DotnetMcpToolBackend_ReadResource_ResolvesStaticResource()
    {
        var backend = CreateDotnetBackend();
        var resource = new McpRegisteredResource
        {
            Id = "execution-status",
            Descriptor = new Resource { Uri = "execution://status", Name = "execution_status" },
            Binding = McpPrimitiveBinding.Create(
                ExecutionMode.Dotnet,
                typeof(ExecutionDotnetMcpResourceStubs).Assembly.Location,
                typeof(ExecutionDotnetMcpResourceStubs).FullName!,
                nameof(ExecutionDotnetMcpResourceStubs.Status)),
        };

        var result = backend.ReadResource(resource, "execution://status", TestContext.CancellationToken);

        Assert.IsNotNull(result.Contents);
        Assert.IsNotEmpty(result.Contents);
    }

    [TestMethod]
    public void DotnetMcpToolBackend_ReadResource_UnknownResource_Throws()
    {
        var backend = CreateDotnetBackend();
        var resource = new McpRegisteredResource
        {
            Id = "missing",
            Descriptor = new Resource { Uri = "execution://missing", Name = "missing_resource" },
            Binding = McpPrimitiveBinding.Create(ExecutionMode.Dotnet, string.Empty, "Missing", "Run"),
        };

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            backend.ReadResource(resource, "execution://missing", TestContext.CancellationToken));
    }

    [TestMethod]
    public void DotnetMcpToolBackend_ClearCaches_AfterResourceRead_AllowsRebind()
    {
        var backend = CreateDotnetBackend();
        var resource = new McpRegisteredResource
        {
            Id = "execution-status-2",
            Descriptor = new Resource { Uri = "execution://status", Name = "execution_status" },
            Binding = McpPrimitiveBinding.Create(
                ExecutionMode.Dotnet,
                typeof(ExecutionDotnetMcpResourceStubs).Assembly.Location,
                typeof(ExecutionDotnetMcpResourceStubs).FullName!,
                nameof(ExecutionDotnetMcpResourceStubs.Status)),
        };

        backend.ReadResource(resource, "execution://status", TestContext.CancellationToken);
        backend.ClearCaches();
        var second = backend.ReadResource(resource, "execution://status", TestContext.CancellationToken);
        Assert.IsNotEmpty(second.Contents);
    }

    [TestMethod]
    public async Task NullDocumentBridge_ReturnsUnavailableResults()
    {
        var open = await NullDocumentBridge.Instance.OpenDocumentAsync("missing.rvt", TestContext.CancellationToken);
        var close = await NullDocumentBridge.Instance.CloseDocumentAsync(save: false, TestContext.CancellationToken);
        var save = await NullDocumentBridge.Instance.SaveDocumentAsync(null, TestContext.CancellationToken);

        Assert.IsFalse(open.Success);
        Assert.IsFalse(close.Success);
        Assert.IsFalse(save.Success);
        Assert.Contains("not available", open.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void BuiltInMcpToolBackend_ReadResource_KnownTemplate_ReturnsPayload()
    {
        var backend = new BuiltInMcpToolBackend([], [new StubBuiltInResource("test://docs/{name}", "docs/{name}", "hello")]);
        var resource = new McpRegisteredResource
        {
            Id = "docs",
            TemplateDescriptor = new ResourceTemplate { UriTemplate = "test://docs/{name}", Name = "docs" },
            Binding = McpPrimitiveBinding.Create(ExecutionMode.CSharp, string.Empty, "docs", "read"),
        };

        var result = backend.ReadResource(resource, "test://docs/readme", TestContext.CancellationToken);
        var text = (TextResourceContents)result.Contents.Single();
        Assert.IsInstanceOfType<TextResourceContents>(text);
        Assert.AreEqual("hello", text.Text);
    }

    [TestMethod]
    public void McpConnectState_RecordsToolCallsAndExecutionScope()
    {
        var state = new McpConnectState(NullLogger<McpConnectState>.Instance);
        state.RecordCall("id-1", "echo");
        state.RecordCall("id-1", "echo");

        Assert.AreEqual(2, state.TotalToolCalls);
        Assert.AreEqual(1, state.ToolCalls.Count);
        Assert.AreEqual(2, state.ToolCalls[0].Count);

        using var scope = state.BeginExecution("echo");
        scope.MarkRunning();
        scope.Dispose();
        Assert.IsFalse(state.IsExecuting);
    }

    [TestMethod]
    public void McpExecutionTracker_ForwardsToConnectState()
    {
        var state = new McpConnectState(NullLogger<McpConnectState>.Instance);
        var tracker = new McpExecutionTracker(state);

        tracker.RecordCall("tool-id", "sample");
        using var scope = tracker.BeginExecution("sample");
        tracker.MarkRunning(scope);
        scope.Dispose();

        Assert.AreEqual(1, state.TotalToolCalls);
    }

    private static DotnetMcpToolBackend CreateDotnetBackend()
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

    private sealed class StubBuiltInResource(string uriTemplate, string protocolUri, string body) : IBuiltInMcpResource
    {
        public string UriTemplate => uriTemplate;

        public Resource ProtocolResource => new() { Uri = protocolUri, Name = "docs" };

        public ReadResourceResult Read(string uri) =>
            new()
            {
                Contents = [new TextResourceContents { Uri = uri, Text = body, MimeType = "text/plain" }],
            };
    }
}

[McpServerResourceType]
internal static class ExecutionDotnetMcpResourceStubs
{
    [McpServerResource(Name = "execution_status")]
    public static TextResourceContents Status() =>
        new() { Uri = "execution://status", Text = "ok", MimeType = "text/plain" };
}

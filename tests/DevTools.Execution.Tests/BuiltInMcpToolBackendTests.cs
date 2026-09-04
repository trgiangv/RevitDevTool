using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.Backends;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Mcp.Catalog;
using DevTools.Mcp.Core.Models;
using ModelContextProtocol.Protocol;
using Moq;

namespace DevTools.Execution.Tests;

public sealed class BuiltInMcpToolBackendTests
{
    [Fact]
    public void SourceKind_IsCSharp()
    {
        var backend = CreateBackend();
        Assert.Equal(ExecutionMode.CSharp, backend.SourceKind);
    }

    [Fact]
    public void ClearCaches_DoesNotThrow()
    {
        var backend = CreateBackend();
        backend.ClearCaches();
    }

    [Fact]
    public async Task InvokeToolAsync_UnknownTool_ReturnsFailure()
    {
        var backend = CreateBackend();
        var tool = new McpRegisteredTool
        {
            Id = "missing",
            Descriptor = new Tool { Name = "missing-tool" },
            Binding = McpPrimitiveBinding.Create(ExecutionMode.CSharp, string.Empty, "tool", "run"),
        };

        var result = await backend.InvokeToolAsync(
            tool,
            new CallToolRequestParams { Name = "missing-tool" },
            ExecutionTestHelpers.InlineHostContext(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains("No built-in tool registered", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokeToolAsync_KnownTool_DelegatesToBuiltInServerTool()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"builtin-open-{Guid.NewGuid():N}.rvt");
        await File.WriteAllTextAsync(tempFile, "stub", TestContext.Current.CancellationToken);

        try
        {
            var bridge = new Mock<IDocumentBridge>();
            bridge
                .Setup(b => b.OpenDocumentAsync(tempFile, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DocumentOperationResult(true, "Opened", "Project1"));

            var openDocument = new OpenDocumentTool(bridge.Object);
            var backend = new BuiltInMcpToolBackend([openDocument], []);
            var tool = new McpRegisteredTool
            {
                Id = openDocument.Name,
                Descriptor = new Tool { Name = openDocument.Name },
                Binding = McpPrimitiveBinding.Create(ExecutionMode.CSharp, string.Empty, "OpenDocumentTool", "Open"),
            };

            var result = await backend.InvokeToolAsync(
                tool,
                new CallToolRequestParams
                {
                    Name = openDocument.Name,
                    Arguments = new Dictionary<string, System.Text.Json.JsonElement>
                    {
                        ["filePath"] = System.Text.Json.JsonSerializer.SerializeToElement(tempFile),
                    },
                },
                ExecutionTestHelpers.InlineHostContext(),
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            bridge.Verify(b => b.OpenDocumentAsync(tempFile, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadResource_UnknownTemplate_Throws()
    {
        var backend = CreateBackend();
        var resource = new McpRegisteredResource
        {
            Id = "missing",
            Descriptor = new Resource { Uri = "test://missing", Name = "missing" },
            Binding = McpPrimitiveBinding.Create(ExecutionMode.CSharp, string.Empty, "res", "read"),
        };

        Assert.Throws<InvalidOperationException>(
            () => backend.ReadResource(resource, "test://missing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ReadResource_KnownTemplate_ReturnsBuiltInPayload()
    {
        var builtInResource = new StubBuiltInResource("test://docs/{name}", "docs/{name}", "hello");
        var backend = new BuiltInMcpToolBackend([], [builtInResource]);
        var resource = new McpRegisteredResource
        {
            Id = "docs",
            TemplateDescriptor = new ResourceTemplate { UriTemplate = "test://docs/{name}", Name = "docs" },
            Binding = McpPrimitiveBinding.Create(ExecutionMode.CSharp, string.Empty, "docs", "read"),
        };

        var result = backend.ReadResource(resource, "test://docs/readme", TestContext.Current.CancellationToken);
        var text = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));
        Assert.Equal("hello", text.Text);
    }

    private static BuiltInMcpToolBackend CreateBackend() =>
        new([], []);

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

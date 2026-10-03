using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.Backends;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Mcp;
using DevTools.Mcp.Core.Models;
using ModelContextProtocol.Protocol;
using Moq;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class BuiltInSourceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SourceKind_IsCSharp()
    {
        var backend = CreateBackend();
        Assert.AreEqual(ExecutionMode.CSharp, backend.SourceKind);
    }

    [TestMethod]
    public void ClearCaches_DoesNotThrow()
    {
        var backend = CreateBackend();
        backend.ClearCaches();
    }

    [TestMethod]
    public async Task InvokeToolAsync_UnknownTool_ReturnsFailure()
    {
        var backend = CreateBackend();
        var tool = new RegisteredTool
        {
            Id = "missing",
            Descriptor = new Tool { Name = "missing-tool" },
            Binding = PrimitiveBinding.Create(ExecutionMode.CSharp, string.Empty, "tool", "run", "", ""),
        };

        var result = await backend.InvokeToolAsync(
            tool,
            new CallToolRequestParams { Name = "missing-tool" },
            ExecutionTestHelpers.InlineHostContext(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsError);
        Assert.Contains("No built-in tool registered", Assert.IsInstanceOfType<TextContentBlock>(result.Content.Single()).Text, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task InvokeToolAsync_KnownTool_DelegatesToBuiltInServerTool()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"builtin-open-{Guid.NewGuid():N}.rvt");
        await File.WriteAllTextAsync(tempFile, "stub", TestContext.CancellationToken);

        try
        {
            var bridge = new Mock<IDocumentBridge>();
            bridge
                .Setup(b => b.OpenDocumentAsync(tempFile, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DocumentOperationResult(true, "Opened", "Project1"));

            var openDocument = new OpenDocumentTool(bridge.Object);
            var backend = new BuiltInSource([openDocument], []);
            var tool = new RegisteredTool
            {
                Id = openDocument.Name,
                Descriptor = new Tool { Name = openDocument.Name },
                Binding = PrimitiveBinding.Create(ExecutionMode.CSharp, string.Empty, "OpenDocumentTool", "Open", "", ""),
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
                TestContext.CancellationToken);

            Assert.AreNotEqual(true, result.IsError);
            bridge.Verify(b => b.OpenDocumentAsync(tempFile, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [TestMethod]
    public async Task ReadResourceAsync_UnknownTemplate_Throws()
    {
        var backend = CreateBackend();
        var resource = new RegisteredResource
        {
            Id = "missing",
            Descriptor = new Resource { Uri = "test://missing", Name = "missing" },
            Binding = PrimitiveBinding.Create(ExecutionMode.CSharp, string.Empty, "res", "read", "", ""),
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            backend.ReadResourceAsync(resource, "test://missing", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadResourceAsync_KnownTemplate_ReturnsBuiltInPayload()
    {
        var builtInResource = new StubBuiltInResource("test://docs/{name}", "docs/{name}", "hello");
        var backend = new BuiltInSource([], [builtInResource]);
        var resource = new RegisteredResource
        {
            Id = "docs",
            TemplateDescriptor = new ResourceTemplate { UriTemplate = "test://docs/{name}", Name = "docs" },
            Binding = PrimitiveBinding.Create(ExecutionMode.CSharp, string.Empty, "docs", "read", "", ""),
        };

        var result = await backend.ReadResourceAsync(resource, "test://docs/readme", TestContext.CancellationToken);
        var text = Assert.IsInstanceOfType<TextResourceContents>(result.Contents.Single());
        Assert.AreEqual("hello", text.Text);
    }

    private static BuiltInSource CreateBackend() =>
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

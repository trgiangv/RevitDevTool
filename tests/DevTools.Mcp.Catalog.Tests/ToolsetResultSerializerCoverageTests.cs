using System.Text.Json;
using DevTools.Mcp.Discovery;
using DevTools.Mcp.Catalog.Tests.Harness;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class ToolsetResultSerializerCoverageTests
{
    [TestMethod]
    public void ToInvocationResponse_NullRaw_ReturnsEmptyContent()
    {
        var result = ResultBridge.ToHostCallToolResult(null, outputSchema: null);

        Assert.IsEmpty(result.Content);
    }

    [TestMethod]
    public void ToInvocationResponse_BoolResult_MarksErrorState()
    {
        var result = ResultBridge.ToHostCallToolResult(true, outputSchema: null);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("true", McpToolInvoke.Text(result));
    }

    [TestMethod]
    public void ToInvocationResponse_ContentBlock_MapsTextBlock()
    {
        var block = new TextContentBlock { Text = "block-text" };

        var result = ResultBridge.ToHostCallToolResult(block, outputSchema: null);

        Assert.AreEqual("block-text", McpToolInvoke.Text(result));
    }

    [TestMethod]
    public void ToInvocationResponse_ResourceLinkBlock_MapsContent()
    {
        var block = new ResourceLinkBlock
        {
            Uri = "sample://demo/item",
            Name = "demo_item",
            Title = "Demo Item",
            Description = "linked resource",
            MimeType = "text/plain",
            Size = 12,
        };

        var result = ResultBridge.ToHostCallToolResult(block, outputSchema: null);

        Assert.HasCount(1, result.Content);
        Assert.IsInstanceOfType<ResourceLinkBlock>(result.Content[0]);
    }

    [TestMethod]
    public void ToInvocationResponse_EmbeddedTextResource_MapsContent()
    {
        var block = new EmbeddedResourceBlock
        {
            Resource = new TextResourceContents
            {
                Uri = "sample://embedded",
                Text = "embedded-body",
                MimeType = "text/plain",
            },
        };

        var result = ResultBridge.ToHostCallToolResult(block, outputSchema: null);

        Assert.HasCount(1, result.Content);
        var embedded = Assert.IsInstanceOfType<EmbeddedResourceBlock>(result.Content[0]);
        Assert.IsInstanceOfType<TextResourceContents>(embedded.Resource);
    }

    [TestMethod]
    public void ToInvocationResponse_InvalidCallToolJson_ThrowsInvalidOperation()
    {
        var invalid = JsonSerializer.SerializeToElement(new { content = new[] { new { type = 123 } } });

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => ResultBridge.ToHostCallToolResult(invalid, outputSchema: null));

        Assert.Contains("SDK contract", ex.Message, StringComparison.Ordinal);
    }
}

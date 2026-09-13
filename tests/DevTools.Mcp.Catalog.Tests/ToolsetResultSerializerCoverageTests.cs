using System.Text.Json;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Catalog.Tests.Harness;
using DevTools.Mcp.Core.Results;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class ToolsetResultSerializerCoverageTests
{
    [TestMethod]
    public void ToInvocationResponse_NullRaw_ReturnsEmptyContent()
    {
        var result = ToolsetResultSerializer.ToInvocationResponse(null, outputSchema: null);

        Assert.IsEmpty(result.Content);
    }

    [TestMethod]
    public void ToInvocationResponse_BoolResult_MarksErrorState()
    {
        var result = ToolsetResultSerializer.ToInvocationResponse(true, outputSchema: null);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("true", McpToolInvoke.Text(result));
    }

    [TestMethod]
    public void ToInvocationResponse_ContentBlock_MapsTextBlock()
    {
        var block = new TextContentBlock { Text = "block-text" };

        var result = ToolsetResultSerializer.ToInvocationResponse(block, outputSchema: null);

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

        var result = ToolsetResultSerializer.ToInvocationResponse(block, outputSchema: null);

        Assert.HasCount(1, result.Content);
        Assert.IsInstanceOfType<McpResourceLinkContent>(result.Content[0]);
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

        var result = ToolsetResultSerializer.ToInvocationResponse(block, outputSchema: null);

        Assert.HasCount(1, result.Content);
        Assert.IsInstanceOfType<McpEmbeddedTextResourceContent>(result.Content[0]);
    }

    [TestMethod]
    public void ToInvocationResponse_InvalidCallToolJson_ThrowsInvalidOperation()
    {
        var invalid = JsonSerializer.SerializeToElement(new { content = new[] { new { type = 123 } } });

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => ToolsetResultSerializer.ToInvocationResponse(invalid, outputSchema: null));

        Assert.Contains("SDK contract", ex.Message, StringComparison.Ordinal);
    }
}

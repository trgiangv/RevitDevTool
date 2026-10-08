using System.Text.Json;
using DevTools.Daemon.Mcp.Code;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Tests;

[TestClass]
public sealed class CodeModeResultTests
{
    [TestMethod]
    public void ToCallToolResult_ImagePassesThrough()
    {
        var image = ImageContentBlock.FromBytes(new byte[] { 1, 2, 3 }, "image/png");
        var source = new CallToolResult { Content = [image] };

        var result = CodeModeResult.ToCallToolResult(source);

        Assert.AreSame(source, result);
        Assert.IsInstanceOfType<ImageContentBlock>(result.Content[0]);
    }

    [TestMethod]
    public void ToCallToolResult_AudioPassesThrough()
    {
        var audio = AudioContentBlock.FromBytes(new byte[] { 9, 8 }, "audio/wav");
        var source = new CallToolResult { Content = [audio] };

        var result = CodeModeResult.ToCallToolResult(source);

        Assert.IsInstanceOfType<AudioContentBlock>(result.Content[0]);
    }

    [TestMethod]
    public void ToCallToolResult_ResourceBlobStaysABlob()
    {
        var read = new ReadResourceResult
        {
            Contents =
            [
                BlobResourceContents.FromBytes(new byte[] { 4, 5 }, "sample://blob", "application/octet-stream"),
            ],
        };

        var result = CodeModeResult.ToCallToolResult(read);

        var embedded = Assert.IsInstanceOfType<EmbeddedResourceBlock>(result.Content[0]);
        Assert.IsInstanceOfType<BlobResourceContents>(embedded.Resource);
    }

    [TestMethod]
    public void ToCallToolResult_AnonymousObjectHasNoImageBlock()
    {
        var result = CodeModeResult.ToCallToolResult(new { category = "Mechanical Equipment", count = 3 });

        Assert.IsEmpty(result.Content.OfType<ImageContentBlock>());
        Assert.IsNull(result.StructuredContent);
        var text = Assert.IsInstanceOfType<TextContentBlock>(result.Content[0]);
        Assert.Contains("Mechanical Equipment", text.Text);
    }

    [TestMethod]
    public void ToCallToolResult_CollectionIsTextAndHasNoStructuredContent()
    {
        var result = CodeModeResult.ToCallToolResult(new[]
        {
            new { name = "execute_csharp_code", processId = 1 },
        });

        Assert.IsNull(result.StructuredContent);
        var text = Assert.IsInstanceOfType<TextContentBlock>(result.Content[0]);
        using var json = JsonDocument.Parse(text.Text);
        Assert.AreEqual(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.AreEqual("execute_csharp_code", json.RootElement[0].GetProperty("name").GetString());
    }

    [TestMethod]
    public void ToCallToolResult_ShortStructuredContentPassesThrough()
    {
        var source = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "Found 1 elements" }],
            StructuredContent = JsonSerializer.SerializeToElement(new { count = 1 }),
        };

        var result = CodeModeResult.ToCallToolResult(source);

        Assert.AreSame(source, result);
        Assert.IsNotNull(result.StructuredContent);
    }

    [TestMethod]
    public void ToCallToolResult_OverOneMebibyteDropsThePayload()
    {
        var image = ImageContentBlock.FromBytes(new byte[CodeModeResult.MaxBytes], "image/png");
        var source = new CallToolResult { Content = [image] };

        var result = CodeModeResult.ToCallToolResult(source);

        Assert.IsTrue(result.IsError);
        Assert.HasCount(1, result.Content);
        Assert.IsInstanceOfType<TextContentBlock>(result.Content[0]);
        Assert.IsFalse(result.Content.OfType<ImageContentBlock>().Any());
    }
}

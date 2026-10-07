using System.Text.Json;
using DevTools.Daemon.Mcp.Code;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Tests;

[TestClass]
public sealed class ProgramResultTests
{
    [TestMethod]
    public void ToCallToolResult_ImagePassesThrough()
    {
        var image = ImageContentBlock.FromBytes(new byte[] { 1, 2, 3 }, "image/png");
        var source = new CallToolResult { Content = [image] };

        var result = ProgramResult.ToCallToolResult(source);

        Assert.AreSame(source, result);
        Assert.IsInstanceOfType<ImageContentBlock>(result.Content[0]);
    }

    [TestMethod]
    public void ToCallToolResult_AudioPassesThrough()
    {
        var audio = AudioContentBlock.FromBytes(new byte[] { 9, 8 }, "audio/wav");
        var source = new CallToolResult { Content = [audio] };

        var result = ProgramResult.ToCallToolResult(source);

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

        var result = ProgramResult.ToCallToolResult(read);

        var embedded = Assert.IsInstanceOfType<EmbeddedResourceBlock>(result.Content[0]);
        Assert.IsInstanceOfType<BlobResourceContents>(embedded.Resource);
    }

    [TestMethod]
    public void ToCallToolResult_AnonymousObjectHasNoImageBlock()
    {
        var result = ProgramResult.ToCallToolResult(new { category = "Mechanical Equipment", count = 3 });

        Assert.IsEmpty(result.Content.OfType<ImageContentBlock>());
        Assert.AreEqual(JsonValueKind.Object, result.StructuredContent!.Value.ValueKind);
        Assert.AreEqual("Mechanical Equipment", result.StructuredContent.Value.GetProperty("category").GetString());
    }

    [TestMethod]
    public void ToCallToolResult_OverOneMebibyteDropsThePayload()
    {
        var image = ImageContentBlock.FromBytes(new byte[ProgramResult.MaxBytes], "image/png");
        var source = new CallToolResult { Content = [image] };

        var result = ProgramResult.ToCallToolResult(source);

        Assert.IsTrue(result.IsError);
        Assert.HasCount(1, result.Content);
        Assert.IsInstanceOfType<TextContentBlock>(result.Content[0]);
        Assert.IsFalse(result.Content.OfType<ImageContentBlock>().Any());
    }
}

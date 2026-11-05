using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class ResultBridgeTests
{
    [TestMethod]
    public void ToHostCallToolResult_OrdinaryTool_KeepsTheSdkResult()
    {
        var sdkResult = Packed(new CallToolResult { Content = [new TextContentBlock { Text = "plain" }] });

        Assert.AreSame(sdkResult, ResultBridge.ToHostCallToolResult(sdkResult, returnsToolsetProtocol: false));
    }

    [TestMethod]
    public void ToHostCallToolResult_ToolsetCallToolResult_ReadsTextStructuredContentAndError()
    {
        var sdkResult = Packed(new CallToolResult
        {
            Content = [new TextContentBlock { Text = "Found 3 elements" }],
            StructuredContent = JsonSerializer.SerializeToElement(new { count = 240 }),
            IsError = true,
        });

        var result = ResultBridge.ToHostCallToolResult(sdkResult, returnsToolsetProtocol: true);

        Assert.AreEqual("Found 3 elements", Assert.IsInstanceOfType<TextContentBlock>(result.Content.Single()).Text);
        Assert.AreEqual(240, result.StructuredContent!.Value.GetProperty("count").GetInt32());
        Assert.IsTrue(result.IsError);
    }

    [TestMethod]
    public void ToHostCallToolResult_ToolsetImageBlock_KeepsBytes()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var sdkResult = Packed<ContentBlock>(ImageContentBlock.FromBytes(png, "image/png"));

        var result = ResultBridge.ToHostCallToolResult(sdkResult, returnsToolsetProtocol: true);

        var image = Assert.IsInstanceOfType<ImageContentBlock>(result.Content.Single());
        Assert.AreEqual("image/png", image.MimeType);
        Assert.IsTrue(image.DecodedData.Span.SequenceEqual(png));
    }

    [TestMethod]
    public void ToHostCallToolResult_ToolsetJsonOffContract_Throws()
    {
        var sdkResult = new CallToolResult { Content = [new TextContentBlock { Text = """{"content":[{"type":"text"}]}""" }] };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => ResultBridge.ToHostCallToolResult(sdkResult, returnsToolsetProtocol: true));

        Assert.Contains("SDK contract", ex.Message, StringComparison.Ordinal);
    }

    private static CallToolResult Packed<T>(T value) =>
        new() { Content = [new TextContentBlock { Text = JsonSerializer.Serialize(value, McpJsonUtilities.DefaultOptions) }] };
}

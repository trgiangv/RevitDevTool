using System.Text.Json;
using DevTools.Mcp.Adapter.Bridging;
using DevTools.Mcp.Adapter;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Catalog.Tests.Harness;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class AlcCallToolResultBridgeReproTests
{
    [TestMethod]
    public void SdkDeserialize_RejectsTextBlockWithoutTextProperty()
    {
        const string envelope =
            """{"content":[{"type":"text"}],"structuredContent":{"healthy":true}}""";

        JsonException? ex = null;
        try
        {
            JsonSerializer.Deserialize<CallToolResult>(envelope, McpJsonUtilities.DefaultOptions);
            Assert.Fail("Expected JsonException.");
        }
        catch (JsonException caught)
        {
            ex = caught;
        }

        Assert.IsNotNull(ex);
        Assert.Contains("Text contents must be provided", ex.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void BridgeForeignCallToolResult_UsesPropertyReflection_PreservesText()
    {
        var foreign = new ForeignMcp.CallToolResult
        {
            Content =
            [
                new ForeignMcp.TextContentBlock { Text = "Model healthy, 0 selected" },
            ],
            StructuredContent = JsonSerializer.SerializeToElement(new { healthy = true }),
            IsError = false,
        };

        var bridged = ToolsetResultSerializer.ToInvocationResponse(foreign, null);
        Assert.AreEqual("Model healthy, 0 selected", McpToolInvoke.Text(bridged));
        Assert.IsTrue(bridged.StructuredContent!.Value.GetProperty("healthy").GetBoolean());
        Assert.IsFalse(bridged.IsError);
    }

    [TestMethod]
    public void BridgeForeignCallToolResult_DoesNotEmitTextlessWireJson()
    {
        var foreign = new ForeignMcp.CallToolResult
        {
            Content = [new ForeignMcp.TextContentBlock { Text = "ok" }],
            StructuredContent = JsonSerializer.SerializeToElement(new { healthy = true }),
        };

        var bridged = ToolsetResultSerializer.ToInvocationResponse(foreign, null);
        var sdk = SdkInvocationMapper.ToSdk(InvocationResponseEncoder.PrepareForWire(bridged));
        var wire = JsonSerializer.Serialize(sdk, McpJsonUtilities.DefaultOptions);

        Assert.Contains("\"text\":\"ok\"", wire, StringComparison.Ordinal);
        Assert.DoesNotContain("""{"type":"text"}""", wire, StringComparison.Ordinal);

        var roundTrip = JsonSerializer.Deserialize<CallToolResult>(wire, McpJsonUtilities.DefaultOptions);
        Assert.HasCount(1, roundTrip!.Content);
        Assert.AreEqual("ok", Assert.IsInstanceOfType<TextContentBlock>(roundTrip.Content[0]).Text);
    }

    [TestMethod]
    public void ToInvocationResponse_RoutesForeignType_ThroughPropertyBridge()
    {
        var foreign = new ForeignMcp.CallToolResult
        {
            Content = [new ForeignMcp.TextContentBlock { Text = "Found 3 elements" }],
            StructuredContent = JsonSerializer.SerializeToElement(new { count = 240 }),
        };

        var outputSchema = JsonSerializer.SerializeToElement(new { type = "object" });
        var result = ToolsetResultSerializer.ToInvocationResponse(foreign, outputSchema);
        Assert.AreEqual("Found 3 elements", McpToolInvoke.Text(result));
        Assert.AreEqual(240, result.StructuredContent!.Value.GetProperty("count").GetInt32());
    }

    [TestMethod]
    public void ToInvocationResponse_BareForeignTextBlock_DoesNotStripText()
    {
        var foreign = new ForeignMcp.TextContentBlock { Text = "bare text block" };
        var result = ToolsetResultSerializer.ToInvocationResponse(foreign, null);
        var sdk = SdkInvocationMapper.ToSdk(result);
        var wire = JsonSerializer.Serialize(sdk, McpJsonUtilities.DefaultOptions);

        Assert.AreEqual("bare text block", McpToolInvoke.Text(result));
        Assert.Contains("\"text\":\"bare text block\"", wire, StringComparison.Ordinal);
    }

    [TestMethod]
    public void BridgeForeignCallToolResult_ImageBlock_PreservesBytes()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var foreign = new ForeignMcp.CallToolResult
        {
            Content =
            [
                new ForeignMcp.ImageContentBlock
                {
                    Data = png,
                    MimeType = "image/png",
                },
            ],
        };

        var bridged = ToolsetResultSerializer.ToInvocationResponse(foreign, null);
        Assert.HasCount(1, bridged.Content);
        var image = Assert.IsInstanceOfType<McpImageContent>(bridged.Content[0]);
        Assert.AreEqual("image/png", image.MimeType);
        Assert.IsTrue(image.Data.AsSpan().SequenceEqual(png));
    }

    [TestMethod]
    public void ToInvocationResponse_ExistingAnonymousAlcFixture_StillWorks()
    {
        var alcShaped = new
        {
            content = new[] { new { type = "text", text = "Found 3 elements (total 240, truncated=true, offset=0)" } },
            structuredContent = new
            {
                count = 240,
                truncated = true,
                elements = new[] { new { id = 1L, category = "Walls" } },
            },
        };

        var outputSchema = JsonSerializer.SerializeToElement(new { type = "object" });
        var result = ToolsetResultSerializer.ToInvocationResponse(alcShaped, outputSchema);
        Assert.Contains("Found 3 elements", McpToolInvoke.Text(result));
        Assert.AreEqual(240, result.StructuredContent!.Value.GetProperty("count").GetInt32());
    }

    [TestMethod]
    public void BridgeForeignCallToolResult_UnsupportedBlock_Throws()
    {
        var foreign = new ForeignMcp.CallToolResult
        {
            Content = [new ForeignMcp.MysteryContentBlock()],
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => ToolsetResultSerializer.ToInvocationResponse(foreign, null));

        Assert.Contains("SDK contract", ex.Message, StringComparison.Ordinal);
    }
}

file static class ForeignMcp
{
    public sealed class CallToolResult
    {
        public List<object> Content { get; set; } = [];
        public JsonElement? StructuredContent { get; set; }
        public bool? IsError { get; set; }
    }

    public sealed class TextContentBlock
    {
        public string Type => "text";
        public string? Text { get; set; }
    }

    public sealed class ImageContentBlock
    {
        public string Type => "image";
        public byte[]? Data { get; set; }
        public string? MimeType { get; set; }
    }

    public sealed class MysteryContentBlock
    {
        public string Type => "mystery";
    }
}

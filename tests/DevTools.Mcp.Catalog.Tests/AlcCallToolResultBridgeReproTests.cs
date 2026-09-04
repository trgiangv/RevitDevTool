using System.Text.Json;
using DevTools.Mcp.Adapter.Bridging;
using DevTools.Mcp.Adapter;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Catalog.Tests.Harness;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class AlcCallToolResultBridgeReproTests
{
    [Fact]
    public void SdkDeserialize_RejectsTextBlockWithoutTextProperty()
    {
        const string envelope =
            """{"content":[{"type":"text"}],"structuredContent":{"healthy":true}}""";

        var ex = Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<CallToolResult>(envelope, McpJsonUtilities.DefaultOptions));
        Assert.Contains("Text contents must be provided", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
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
        Assert.Equal("Model healthy, 0 selected", McpToolInvoke.Text(bridged));
        Assert.True(bridged.StructuredContent!.Value.GetProperty("healthy").GetBoolean());
        Assert.False(bridged.IsError);
    }

    [Fact]
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
        Assert.Equal("ok", Assert.IsType<TextContentBlock>(Assert.Single(roundTrip!.Content)).Text);
    }

    [Fact]
    public void ToInvocationResponse_RoutesForeignType_ThroughPropertyBridge()
    {
        var foreign = new ForeignMcp.CallToolResult
        {
            Content = [new ForeignMcp.TextContentBlock { Text = "Found 3 elements" }],
            StructuredContent = JsonSerializer.SerializeToElement(new { count = 240 }),
        };

        var outputSchema = JsonSerializer.SerializeToElement(new { type = "object" });
        var result = ToolsetResultSerializer.ToInvocationResponse(foreign, outputSchema);
        Assert.Equal("Found 3 elements", McpToolInvoke.Text(result));
        Assert.Equal(240, result.StructuredContent!.Value.GetProperty("count").GetInt32());
    }

    [Fact]
    public void ToInvocationResponse_BareForeignTextBlock_DoesNotStripText()
    {
        var foreign = new ForeignMcp.TextContentBlock { Text = "bare text block" };
        var result = ToolsetResultSerializer.ToInvocationResponse(foreign, null);
        var sdk = SdkInvocationMapper.ToSdk(result);
        var wire = JsonSerializer.Serialize(sdk, McpJsonUtilities.DefaultOptions);

        Assert.Equal("bare text block", McpToolInvoke.Text(result));
        Assert.Contains("\"text\":\"bare text block\"", wire, StringComparison.Ordinal);
    }

    [Fact]
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
        var image = Assert.IsType<McpImageContent>(Assert.Single(bridged.Content));
        Assert.Equal("image/png", image.MimeType);
        Assert.True(image.Data.AsSpan().SequenceEqual(png));
    }

    [Fact]
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
        Assert.Equal(240, result.StructuredContent!.Value.GetProperty("count").GetInt32());
    }

    [Fact]
    public void BridgeForeignCallToolResult_UnsupportedBlock_Throws()
    {
        var foreign = new ForeignMcp.CallToolResult
        {
            Content = [new ForeignMcp.MysteryContentBlock()],
        };

        var ex = Assert.Throws<InvalidOperationException>(
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

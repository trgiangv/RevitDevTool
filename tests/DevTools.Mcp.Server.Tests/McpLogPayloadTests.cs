using System.Text.Json;
using DevTools.Mcp.Server.Hosting;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Server.Tests;

public class McpLogPayloadTests
{
    [Fact]
    public void SerializeArgs_NullOrEmpty_ReturnsEmptyObject()
    {
        Assert.Equal("{}", McpLogPayload.SerializeArgs(null));
        Assert.Equal("{}", McpLogPayload.SerializeArgs([]));
    }

    [Fact]
    public void SerializeArgs_SerializesDictionary()
    {
        var args = new Dictionary<string, JsonElement>
        {
            ["topic"] = JsonSerializer.SerializeToElement("demo"),
            ["count"] = JsonSerializer.SerializeToElement(3),
        };

        var json = McpLogPayload.SerializeArgs(args);

        Assert.Contains("\"topic\":\"demo\"", json, StringComparison.Ordinal);
        Assert.Contains("\"count\":3", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeCallToolResult_StructuredWithoutBinary_ReturnsStructuredRawText()
    {
        var result = new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToElement(new { healthy = true }),
            Content = [new TextContentBlock { Text = "ignored when structured-only" }],
        };

        var json = McpLogPayload.SerializeCallToolResult(result);

        Assert.Contains("\"healthy\":true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeCallToolResult_ImageContent_RedactsBinaryLength()
    {
        var result = new CallToolResult
        {
            Content = [ImageContentBlock.FromBytes(new byte[] { 0x01, 0x02, 0x03 }, "image/png")],
        };

        var json = McpLogPayload.SerializeCallToolResult(result);

        Assert.Contains("\"type\":\"image\"", json, StringComparison.Ordinal);
        Assert.Contains("\"length\":3", json, StringComparison.Ordinal);
        Assert.DoesNotContain("AQID", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeReadResourceResult_RedactsBlobLength()
    {
        var result = new ReadResourceResult
        {
            Contents = [BlobResourceContents.FromBytes(new byte[] { 0x10, 0x20 }, "file://demo", "application/octet-stream")],
        };

        var json = McpLogPayload.SerializeReadResourceResult(result);

        Assert.Contains("\"type\":\"blob\"", json, StringComparison.Ordinal);
        Assert.Contains("\"length\":2", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeCallToolResult_ScrubsTextWrappedBlobResource()
    {
        var blobBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var resource = new ReadResourceResult
        {
            Contents =
            [
                BlobResourceContents.FromBytes(blobBytes, "image://test", "image/png")
            ]
        };

        var wrapped = ToolHelpers.Serialize(resource);
        var callResult = ToolHelpers.Result(wrapped);
        var logJson = McpLogPayload.SerializeCallToolResult(callResult);

        Assert.Contains("\"type\":\"blob\"", logJson, StringComparison.Ordinal);
        Assert.Contains("\"length\":4", logJson, StringComparison.Ordinal);
        Assert.DoesNotContain("3q2+7w==", logJson, StringComparison.Ordinal);
    }
}

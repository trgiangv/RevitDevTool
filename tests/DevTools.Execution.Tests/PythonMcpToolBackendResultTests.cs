using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Execution.External.Mcp.Backends;
using ModelContextProtocol.Protocol;

namespace DevTools.Execution.Tests;

public sealed class PythonMcpToolBackendResultTests
{
    [Fact]
    public void PythonResultParser_PreservesNativeSdkResponseSemantics()
    {
        var resource = BlobResourceContents.FromBytes(new byte[] { 8, 9 }, "test://python", "application/octet-stream");
        resource.Meta = new JsonObject { ["resource"] = "meta" };
        var expected = new CallToolResult
        {
            IsError = true,
            StructuredContent = JsonDocument.Parse("{\"ok\":false}").RootElement.Clone(),
            Meta = new JsonObject { ["response"] = "meta" },
            Content =
            [
                new TextContentBlock { Text = "failure", Meta = new JsonObject { ["content"] = "meta" } },
                ImageContentBlock.FromBytes(new byte[] { 1, 2 }, "image/png"),
                new EmbeddedResourceBlock { Resource = resource }
            ]
        };

        var actual = PythonMcpToolBackend.ReadToolResult(
            JsonSerializer.Serialize(expected, ModelContextProtocol.McpJsonUtilities.DefaultOptions));

        Assert.True(actual.IsError);
        Assert.Equal("meta", actual.Meta!["response"]!.GetValue<string>());
        Assert.Equal("{\"ok\":false}", actual.StructuredContent!.Value.GetRawText());
        Assert.Equal("meta", actual.Content[0].Meta!["content"]!.GetValue<string>());
        Assert.Equal(new byte[] { 1, 2 }, ((ImageContentBlock)actual.Content[1]).DecodedData.ToArray());
        var blob = Assert.IsType<BlobResourceContents>(((EmbeddedResourceBlock)actual.Content[2]).Resource);
        Assert.Equal("test://python", blob.Uri);
        Assert.Equal("application/octet-stream", blob.MimeType);
        Assert.Equal(new byte[] { 8, 9 }, blob.DecodedData.ToArray());
        Assert.Equal("meta", blob.Meta!["resource"]!.GetValue<string>());
    }
}

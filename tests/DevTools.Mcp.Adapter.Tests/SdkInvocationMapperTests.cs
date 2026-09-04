using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Adapter.Bridging;
using DevTools.Mcp.Core.Protocol.Invocation;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Adapter.Tests;

public sealed class SdkInvocationMapperTests
{
    [Fact]
    public void SdkInvocationMapper_ToSdk_PreservesEverySupportedContentShape()
    {
        var annotations = new Annotations { Priority = 0.5f };
        var blob = BlobResourceContents.FromBytes(new byte[] { 6, 7 }, "test://blob", "application/octet-stream");
        blob.Meta = new JsonObject { ["resource"] = "blob" };
        var response = new McpInvocationResponse
        {
            IsError = true,
            StructuredContent = JsonDocument.Parse("{\"answer\":42}").RootElement.Clone(),
            Meta = new JsonObject { ["response"] = "meta" },
            Content =
            [
                new McpTextContent("text") { Annotations = annotations, Meta = new JsonObject { ["text"] = 1 } },
                new McpImageContent(new byte[] { 1, 2, 3 }, "image/png"),
                new McpAudioContent(new byte[] { 4, 5 }, "audio/wav"),
                new McpEmbeddedTextResourceContent("test://text", "resource", "text/plain") { ResourceMeta = new JsonObject { ["resource"] = "text" } },
                new McpEmbeddedBlobResourceContent("test://blob", new byte[] { 6, 7 }, "application/octet-stream") { ResourceMeta = new JsonObject { ["resource"] = "blob" } },
                new McpResourceLinkContent("test://link", "link", "Link title", "A linked resource", "text/plain", 42) { Meta = new JsonObject { ["link"] = 1 } }
            ]
        };

        var sdk = SdkInvocationMapper.ToSdk(response);

        Assert.True(sdk.IsError);
        Assert.Equal("meta", sdk.Meta!["response"]!.GetValue<string>());
        Assert.Equal("{\"answer\":42}", sdk.StructuredContent!.Value.GetRawText());
        Assert.Equal(6, sdk.Content.Count);
        Assert.Equal(0.5f, sdk.Content[0].Annotations!.Priority);
        Assert.Equal("text", ((TextContentBlock)sdk.Content[0]).Text);
        Assert.Equal([1, 2, 3], ((ImageContentBlock)sdk.Content[1]).DecodedData.ToArray());
        Assert.Equal([4, 5], ((AudioContentBlock)sdk.Content[2]).DecodedData.ToArray());
        var textResource = Assert.IsType<TextResourceContents>(((EmbeddedResourceBlock)sdk.Content[3]).Resource);
        Assert.Equal("test://text", textResource.Uri);
        Assert.Equal("resource", textResource.Text);
        Assert.Equal("text", textResource.Meta!["resource"]!.GetValue<string>());
        var blobResource = Assert.IsType<BlobResourceContents>(((EmbeddedResourceBlock)sdk.Content[4]).Resource);
        Assert.Equal("test://blob", blobResource.Uri);
        Assert.Equal("application/octet-stream", blobResource.MimeType);
        Assert.Equal([6, 7], blobResource.DecodedData.ToArray());
        Assert.Equal("blob", blobResource.Meta!["resource"]!.GetValue<string>());
        var resourceLink = Assert.IsType<ResourceLinkBlock>(sdk.Content[5]);
        Assert.Equal("test://link", resourceLink.Uri);
        Assert.Equal("link", resourceLink.Name);
        Assert.Equal("Link title", resourceLink.Title);
        Assert.Equal("A linked resource", resourceLink.Description);
        Assert.Equal("text/plain", resourceLink.MimeType);
        Assert.Equal(42, resourceLink.Size);
        Assert.Equal(1, resourceLink.Meta!["link"]!.GetValue<int>());
    }
}

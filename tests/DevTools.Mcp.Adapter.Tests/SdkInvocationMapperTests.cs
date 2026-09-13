using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Adapter.Bridging;
using DevTools.Mcp.Core.Protocol.Invocation;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Adapter.Tests;

[TestClass]
public sealed class SdkInvocationMapperTests
{
    [TestMethod]
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

        Assert.IsTrue(sdk.IsError);
        Assert.AreEqual("meta", sdk.Meta!["response"]!.GetValue<string>());
        Assert.AreEqual("{\"answer\":42}", sdk.StructuredContent!.Value.GetRawText());
        Assert.AreEqual(6, sdk.Content.Count);
        Assert.AreEqual(0.5f, sdk.Content[0].Annotations!.Priority);
        Assert.AreEqual("text", ((TextContentBlock)sdk.Content[0]).Text);
        Assert.AreSequenceEqual(new byte[] { 1, 2, 3 }, ((ImageContentBlock)sdk.Content[1]).DecodedData.ToArray());
        Assert.AreSequenceEqual(new byte[] { 4, 5 }, ((AudioContentBlock)sdk.Content[2]).DecodedData.ToArray());
        var textResource = Assert.IsInstanceOfType<TextResourceContents>(((EmbeddedResourceBlock)sdk.Content[3]).Resource);
        Assert.AreEqual("test://text", textResource.Uri);
        Assert.AreEqual("resource", textResource.Text);
        Assert.AreEqual("text", textResource.Meta!["resource"]!.GetValue<string>());
        var blobResource = Assert.IsInstanceOfType<BlobResourceContents>(((EmbeddedResourceBlock)sdk.Content[4]).Resource);
        Assert.AreEqual("test://blob", blobResource.Uri);
        Assert.AreEqual("application/octet-stream", blobResource.MimeType);
        Assert.AreSequenceEqual(new byte[] { 6, 7 }, blobResource.DecodedData.ToArray());
        Assert.AreEqual("blob", blobResource.Meta!["resource"]!.GetValue<string>());
        var resourceLink = Assert.IsInstanceOfType<ResourceLinkBlock>(sdk.Content[5]);
        Assert.AreEqual("test://link", resourceLink.Uri);
        Assert.AreEqual("link", resourceLink.Name);
        Assert.AreEqual("Link title", resourceLink.Title);
        Assert.AreEqual("A linked resource", resourceLink.Description);
        Assert.AreEqual("text/plain", resourceLink.MimeType);
        Assert.AreEqual(42, resourceLink.Size);
        Assert.AreEqual(1, resourceLink.Meta!["link"]!.GetValue<int>());
    }
}

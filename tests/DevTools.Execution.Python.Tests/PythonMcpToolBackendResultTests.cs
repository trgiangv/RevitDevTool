using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Execution.External.Mcp.Backends;
using ModelContextProtocol.Protocol;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PythonMcpToolBackendResultTests
{
    [TestMethod]
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

        Assert.IsTrue(actual.IsError);
        Assert.AreEqual("meta", actual.Meta!["response"]!.GetValue<string>());
        Assert.AreEqual("{\"ok\":false}", actual.StructuredContent!.Value.GetRawText());
        Assert.AreEqual("meta", actual.Content[0].Meta!["content"]!.GetValue<string>());
        Assert.AreSequenceEqual(new byte[] { 1, 2 }, ((ImageContentBlock)actual.Content[1]).DecodedData.ToArray());
        var blob = (BlobResourceContents)((EmbeddedResourceBlock)actual.Content[2]).Resource;
        Assert.IsInstanceOfType(blob, typeof(BlobResourceContents));
        Assert.AreEqual("test://python", blob.Uri);
        Assert.AreEqual("application/octet-stream", blob.MimeType);
        Assert.AreSequenceEqual(new byte[] { 8, 9 }, blob.DecodedData.ToArray());
        Assert.AreEqual("meta", blob.Meta!["resource"]!.GetValue<string>());
    }
}

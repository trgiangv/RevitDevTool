using System.Text.Json.Nodes;
using ContentKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Content;
using ResourcesKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Resources;

namespace DevTools.Mcp.Core.Protocol;

/// <summary>Encodes <see cref="McpReadResourceResponse"/> to MCP <c>resources/read</c> wire shape.</summary>
public static class ReadResourceEncoder
{
    public static JsonNode ToNode(McpReadResourceResponse response)
    {
        var contents = new JsonArray();
        foreach (var item in response.Contents)
            contents.Add(WriteContent(item));

        return new JsonObject { ["contents"] = contents };
    }

    private static JsonObject WriteContent(McpReadResourceContent content) =>
        content switch
        {
            McpReadResourceTextContent text => new JsonObject
            {
                [ResourcesKeys.Uri] = text.Uri,
                [ResourcesKeys.MimeType] = text.MimeType,
                [ContentKeys.Text] = text.Text,
            },
            McpReadResourceBlobContent blob => new JsonObject
            {
                [ResourcesKeys.Uri] = blob.Uri,
                [ResourcesKeys.MimeType] = blob.MimeType,
                ["blob"] = Convert.ToBase64String(blob.Blob),
            },
            _ => new JsonObject { [ResourcesKeys.Uri] = content.Uri },
        };
}

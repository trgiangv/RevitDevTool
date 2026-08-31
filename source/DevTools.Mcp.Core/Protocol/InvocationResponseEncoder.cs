using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Invocation;
using ContentKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Content;
using BlockTypes = DevTools.Mcp.Core.Protocol.McpSpecKeys.ContentBlockTypes;
using ResourcesKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Resources;
using ToolResultKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.ToolResult;

namespace DevTools.Mcp.Core.Protocol;

/// <summary>Encodes <see cref="McpInvocationResponse"/> to MCP <c>tools/call</c> wire shape without SDK types.</summary>
public static class InvocationResponseEncoder
{
    private const int MaxPreviewLength = 240;
    private const string Ellipsis = "...";
    private static readonly int PreviewPrefixLength = MaxPreviewLength - Ellipsis.Length;

    public static JsonNode ToNode(McpInvocationResponse response)
    {
        var safe = PrepareForWire(response);
        var result = new JsonObject();

        if (safe.Content.Count > 0)
        {
            var content = new JsonArray();
            foreach (var block in safe.Content)
                content.Add(WriteContentBlock(block));
            result[ToolResultKeys.Content] = content;
        }

        if (safe.IsError is { } isError)
            result[ToolResultKeys.IsError] = isError;

        if (safe.StructuredContent is { } structured)
            result[ToolResultKeys.StructuredContent] = JsonNode.Parse(structured.GetRawText());

        if (safe.Meta is not null)
            result[McpSpecKeys.Meta.Key] = safe.Meta.DeepClone();

        return result;
    }

    public static McpInvocationResponse PrepareForWire(McpInvocationResponse response)
    {
        var content = response.Content.ToList();
        for (var i = 0; i < content.Count; i++)
        {
            if (content[i] is not McpTextContent text || !string.IsNullOrEmpty(text.Text))
                continue;

            var fallback = response.StructuredContent is { } structured
                ? PreviewStructured(structured)
                : "{}";
            content[i] = text with { Text = fallback };
        }

        if (content.Count == 0 && response.StructuredContent is { } onlyStructured)
            content.Add(new McpTextContent(PreviewStructured(onlyStructured)));

        return response with { Content = content };
    }

    public static string PreviewStructured(JsonElement structured)
    {
        var raw = structured.GetRawText();
        return raw.Length <= MaxPreviewLength ? raw : raw[..PreviewPrefixLength] + Ellipsis;
    }

    private static JsonObject WriteContentBlock(McpContent content)
    {
        var block = content switch
        {
            McpTextContent text => new JsonObject
            {
                [ContentKeys.Type] = BlockTypes.Text,
                [ContentKeys.Text] = text.Text,
            },
            McpImageContent image => new JsonObject
            {
                [ContentKeys.Type] = BlockTypes.Image,
                ["data"] = Convert.ToBase64String(image.Data),
                [ResourcesKeys.MimeType] = image.MimeType,
            },
            McpAudioContent audio => new JsonObject
            {
                [ContentKeys.Type] = BlockTypes.Audio,
                ["data"] = Convert.ToBase64String(audio.Data),
                [ResourcesKeys.MimeType] = audio.MimeType,
            },
            McpEmbeddedTextResourceContent textResource => new JsonObject
            {
                [ContentKeys.Type] = BlockTypes.Resource,
                ["resource"] = WriteTextResource(textResource),
            },
            McpEmbeddedBlobResourceContent blobResource => new JsonObject
            {
                [ContentKeys.Type] = BlockTypes.Resource,
                ["resource"] = WriteBlobResource(blobResource),
            },
            McpResourceLinkContent link => new JsonObject
            {
                [ContentKeys.Type] = BlockTypes.ResourceLink,
                [ResourcesKeys.Uri] = link.Uri,
                ["name"] = link.Name,
                ["title"] = link.Title,
                ["description"] = link.Description,
                [ResourcesKeys.MimeType] = link.MimeType,
                [ResourcesKeys.Size] = link.Size,
            },
            _ => new JsonObject
            {
                [ContentKeys.Type] = BlockTypes.Text,
                [ContentKeys.Text] = string.Empty,
            },
        };

        WriteOptionalMetadata(block, content);
        return block;
    }

    private static JsonObject WriteTextResource(McpEmbeddedTextResourceContent resource)
    {
        var obj = new JsonObject
        {
            [ResourcesKeys.Uri] = resource.Uri,
            [ContentKeys.Text] = resource.Text,
            [ResourcesKeys.MimeType] = resource.MimeType,
        };
        if (resource.ResourceMeta is not null)
            obj[McpSpecKeys.Meta.Key] = resource.ResourceMeta.DeepClone();
        return obj;
    }

    private static JsonObject WriteBlobResource(McpEmbeddedBlobResourceContent resource)
    {
        var obj = new JsonObject
        {
            [ResourcesKeys.Uri] = resource.Uri,
            ["blob"] = Convert.ToBase64String(resource.Blob),
            [ResourcesKeys.MimeType] = resource.MimeType,
        };
        if (resource.ResourceMeta is not null)
            obj[McpSpecKeys.Meta.Key] = resource.ResourceMeta.DeepClone();
        return obj;
    }

    private static void WriteOptionalMetadata(JsonObject block, McpContent content)
    {
        if (content.Annotations is not null)
            block["annotations"] = JsonSerializer.SerializeToNode(content.Annotations);

        if (content.Meta is not null)
            block[McpSpecKeys.Meta.Key] = content.Meta.DeepClone();
    }
}

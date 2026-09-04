using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Invocation;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Adapter.Bridging;

/// <summary>Maps host invocation DTOs to SDK <see cref="CallToolResult"/>.</summary>
public static class SdkInvocationMapper
{
    public static CallToolResult ToSdk(McpInvocationResponse response) => new()
    {
        Content = response.Content.Select(ToSdk).ToList(),
        IsError = response.IsError,
        StructuredContent = response.StructuredContent?.Clone(),
        Meta = Clone(response.Meta)
    };

    private static ContentBlock ToSdk(McpContent content) => content switch
    {
        McpTextContent text => WithProtocolMetadata(new TextContentBlock { Text = text.Text }, text),
        McpImageContent image => WithProtocolMetadata(ImageContentBlock.FromBytes(image.Data, image.MimeType), image),
        McpAudioContent audio => WithProtocolMetadata(AudioContentBlock.FromBytes(audio.Data, audio.MimeType), audio),
        McpEmbeddedTextResourceContent resource => WithProtocolMetadata(new EmbeddedResourceBlock { Resource = new TextResourceContents { Uri = resource.Uri, Text = resource.Text, MimeType = resource.MimeType, Meta = Clone(resource.ResourceMeta) } }, resource),
        McpEmbeddedBlobResourceContent resource => WithProtocolMetadata(new EmbeddedResourceBlock { Resource = CreateBlobResource(resource) }, resource),
        McpResourceLinkContent link => WithProtocolMetadata(new ResourceLinkBlock { Uri = link.Uri, Name = link.Name, Title = link.Title, Description = link.Description, MimeType = link.MimeType, Size = link.Size }, link),
        _ => throw new NotSupportedException($"Unsupported DevTools MCP content '{content.GetType().FullName}'.")
    };

    private static BlobResourceContents CreateBlobResource(McpEmbeddedBlobResourceContent resource)
    {
        var blob = BlobResourceContents.FromBytes(resource.Blob, resource.Uri, resource.MimeType);
        blob.Meta = Clone(resource.ResourceMeta);
        return blob;
    }

    private static T WithProtocolMetadata<T>(T content, McpContent source) where T : ContentBlock
    {
        content.Annotations = source.Annotations;
        content.Meta = Clone(source.Meta);
        return content;
    }

    private static JsonObject? Clone(JsonObject? value) => value?.DeepClone().AsObject();
}

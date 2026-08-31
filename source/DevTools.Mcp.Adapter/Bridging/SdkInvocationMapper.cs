using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Adapter.Bridging;

/// <summary>Maps between host invocation DTOs and SDK <see cref="CallToolResult"/>.</summary>
public static class SdkInvocationMapper
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;

    public static McpInvocationResponse ToCore(CallToolResult result) => new()
    {
        Content = result.Content.Select(ToCore).ToArray(),
        IsError = result.IsError,
        StructuredContent = result.StructuredContent?.Clone(),
        Meta = Clone(result.Meta)
    };

    public static CallToolResult ToSdk(McpInvocationResponse response) => new()
    {
        Content = response.Content.Select(ToSdk).ToList(),
        IsError = response.IsError,
        StructuredContent = response.StructuredContent?.Clone(),
        Meta = Clone(response.Meta)
    };

    private static CallToolResult RoundTripSdk(CallToolResult result)
    {
        var element = JsonSerializer.SerializeToElement(EnsureWireSafeSdk(result), JsonOptions);
        var roundTripped = element.Deserialize<CallToolResult>(JsonOptions) ?? result;
        return EnsureWireSafeSdk(roundTripped);
    }

    public static McpInvocationResponse RoundTripCore(McpInvocationResponse response) =>
        ToCore(RoundTripSdk(ToSdk(response)));

    private static CallToolResult EnsureWireSafeSdk(CallToolResult result)
    {
        if (result.Content.Count == 0)
        {
            if (result.StructuredContent is { } structured)
                result.Content = [new TextContentBlock { Text = InvocationResponseEncoder.PreviewStructured(structured) }];
            return result;
        }

        for (var i = 0; i < result.Content.Count; i++)
        {
            if (result.Content[i] is not TextContentBlock text || !string.IsNullOrEmpty(text.Text))
                continue;

            var fallback = result.StructuredContent is { } structured
                ? InvocationResponseEncoder.PreviewStructured(structured)
                : "{}";
            result.Content[i] = new TextContentBlock
            {
                Text = fallback,
                Annotations = text.Annotations,
                Meta = text.Meta,
            };
        }

        return result;
    }

    private static McpContent ToCore(ContentBlock content) => content switch
    {
        TextContentBlock text => new McpTextContent(text.Text) { Annotations = text.Annotations, Meta = Clone(text.Meta) },
        ImageContentBlock image => new McpImageContent(image.DecodedData.ToArray(), image.MimeType) { Annotations = image.Annotations, Meta = Clone(image.Meta) },
        AudioContentBlock audio => new McpAudioContent(audio.DecodedData.ToArray(), audio.MimeType) { Annotations = audio.Annotations, Meta = Clone(audio.Meta) },
        EmbeddedResourceBlock { Resource: TextResourceContents resource } => new McpEmbeddedTextResourceContent(resource.Uri, resource.Text, resource.MimeType) { Annotations = content.Annotations, Meta = Clone(content.Meta), ResourceMeta = Clone(resource.Meta) },
        EmbeddedResourceBlock { Resource: BlobResourceContents resource } => new McpEmbeddedBlobResourceContent(resource.Uri, resource.DecodedData.ToArray(), resource.MimeType) { Annotations = content.Annotations, Meta = Clone(content.Meta), ResourceMeta = Clone(resource.Meta) },
        ResourceLinkBlock link => new McpResourceLinkContent(link.Uri, link.Name, link.Title, link.Description, link.MimeType, link.Size) { Annotations = link.Annotations, Meta = Clone(link.Meta) },
        ToolUseContentBlock => throw new NotSupportedException("ToolUseContentBlock cannot be represented by a primitive invocation response."),
        ToolResultContentBlock => throw new NotSupportedException("ToolResultContentBlock cannot be represented by a primitive invocation response."),
        _ => throw new NotSupportedException($"Unsupported MCP content block '{content.GetType().FullName}'.")
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

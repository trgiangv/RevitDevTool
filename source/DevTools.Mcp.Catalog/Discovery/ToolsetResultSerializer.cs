using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Discovery;

/// <summary>
/// Normalizes invoke results through the MCP SDK JSON contract.
/// The runtime type is serialized before host deserialization so an isolated SDK
/// identity never needs a reflected content-block mapper.
/// </summary>
public static class ToolsetResultSerializer
{
    public static McpInvocationResponse ToInvocationResponse(object? raw, JsonElement? outputSchema)
    {
        if (raw is null)
            return new McpInvocationResponse { Content = [] };

        if (raw is CallToolResult hostResult && ReferenceEquals(raw.GetType(), typeof(CallToolResult)))
            return MapHostCallToolResult(hostResult);

        if (raw is ContentBlock hostBlock)
        {
            return new McpInvocationResponse
            {
                Content = [MapHostContentBlock(hostBlock)],
            };
        }

        var element = SerializeRuntime(raw);
        if (TryReadCallToolResult(element, out var bridged))
        {
            return MapHostCallToolResult(bridged);
        }

        if (TryReadContentBlock(element, out var contentBlock))
            return new McpInvocationResponse { Content = [MapHostContentBlock(contentBlock)] };

        return MapPlainResult(raw, element, outputSchema);
    }

    private static bool TryReadCallToolResult(JsonElement element, out CallToolResult result)
    {
        result = null!;
        if (element.ValueKind != JsonValueKind.Object ||
            (!element.TryGetProperty("content", out _) && !element.TryGetProperty("Content", out _)))
            return false;

        try
        {
            result = element.Deserialize<CallToolResult>(ToolHelpers.ProtocolOptions)!;
            return result is not null;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("MCP tool result JSON did not match the SDK contract.", ex);
        }
    }

    private static JsonElement SerializeRuntime(object value)
    {
        // Serialize with the runtime type so an SDK object loaded from another
        // ALC/ILRepack identity is reflected by its own properties. The SDK
        // options remain the contract used when reading the normalized JSON.
        return JsonSerializer.SerializeToElement(value, value.GetType(), ToolHelpers.RuntimeJsonOptions);
    }

    private static bool TryReadContentBlock(JsonElement element, out ContentBlock block)
    {
        block = null!;
        if (element.ValueKind != JsonValueKind.Object ||
            (!element.TryGetProperty("type", out _) && !element.TryGetProperty("Type", out _)))
            return false;

        try
        {
            block = element.Deserialize<ContentBlock>(ToolHelpers.ProtocolOptions)!;
            return block is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static McpInvocationResponse MapHostCallToolResult(CallToolResult result) =>
        new()
        {
            Content = result.Content.Select(MapHostContentBlock).ToArray(),
            IsError = result.IsError,
            StructuredContent = result.StructuredContent?.Clone(),
            Meta = CloneJsonObject(result.Meta),
        };

    private static McpContent MapHostContentBlock(ContentBlock block) => block switch
    {
        TextContentBlock text => new McpTextContent(text.Text)
        {
            Annotations = text.Annotations,
            Meta = CloneJsonObject(text.Meta),
        },
        ImageContentBlock image => new McpImageContent(image.DecodedData.ToArray(), image.MimeType)
        {
            Annotations = image.Annotations,
            Meta = CloneJsonObject(image.Meta),
        },
        AudioContentBlock audio => new McpAudioContent(audio.DecodedData.ToArray(), audio.MimeType)
        {
            Annotations = audio.Annotations,
            Meta = CloneJsonObject(audio.Meta),
        },
        EmbeddedResourceBlock { Resource: TextResourceContents textResource } =>
            new McpEmbeddedTextResourceContent(textResource.Uri, textResource.Text, textResource.MimeType)
            {
                Annotations = block.Annotations,
                Meta = CloneJsonObject(block.Meta),
                ResourceMeta = CloneJsonObject(textResource.Meta),
            },
        EmbeddedResourceBlock { Resource: BlobResourceContents blobResource } =>
            new McpEmbeddedBlobResourceContent(blobResource.Uri, blobResource.DecodedData.ToArray(), blobResource.MimeType)
            {
                Annotations = block.Annotations,
                Meta = CloneJsonObject(block.Meta),
                ResourceMeta = CloneJsonObject(blobResource.Meta),
            },
        ResourceLinkBlock link => new McpResourceLinkContent(
            link.Uri,
            link.Name,
            link.Title,
            link.Description,
            link.MimeType,
            link.Size)
        {
            Annotations = link.Annotations,
            Meta = CloneJsonObject(link.Meta),
        },
        _ => throw new NotSupportedException($"Unsupported host content block '{block.GetType().FullName}'."),
    };

    private static McpInvocationResponse MapPlainResult(
        object raw,
        JsonElement element,
        JsonElement? outputSchema)
    {
        switch (raw)
        {
            case string text:
                return new McpInvocationResponse
                {
                    Content = [new McpTextContent(text)],
                    StructuredContent = CreateStructuredContent(outputSchema, element),
                };
            case bool isError:
                return new McpInvocationResponse
                {
                    IsError = isError,
                    Content = [new McpTextContent(element.GetRawText())],
                    StructuredContent = CreateStructuredContent(outputSchema, element),
                };
            default:
                return new McpInvocationResponse
                {
                    Content = [new McpTextContent(element.GetRawText())],
                    StructuredContent = CreateStructuredContent(outputSchema, element),
                };
        }
    }

    private static JsonElement? CreateStructuredContent(JsonElement? outputSchema, JsonElement naturalValue)
    {
        if (outputSchema is null)
            return null;

        return naturalValue.Clone();
    }

    private static JsonObject? CloneJsonObject(object? value) =>
        value switch
        {
            null => null,
            JsonObject obj => obj.DeepClone().AsObject(),
            JsonElement { ValueKind: JsonValueKind.Object } element =>
                JsonNode.Parse(element.GetRawText())?.AsObject(),
            _ => null,
        };
}

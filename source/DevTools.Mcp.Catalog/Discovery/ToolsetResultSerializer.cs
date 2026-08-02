using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ToolResultKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.ToolResult;
using BlockTypes = DevTools.Mcp.Core.Protocol.McpSpecKeys.ContentBlockTypes;
namespace DevTools.Mcp.Catalog.Discovery;

/// <summary>
/// Maps raw toolset invoke results to host-owned <see cref="McpInvocationResponse"/>.
/// Handles ILRepacked MCP types without STJ-serializing foreign <see cref="ContentBlock"/> graphs.
/// </summary>
public static class ToolsetResultSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;

    public static McpInvocationResponse ToInvocationResponse(object? raw, JsonElement? outputSchema)
    {
        if (raw is null)
            return new McpInvocationResponse { Content = [] };

        if (raw is CallToolResult hostResult && ReferenceEquals(raw.GetType(), typeof(CallToolResult)))
            return EnsureWireSafe(MapHostCallToolResult(hostResult));

        if (IsForeignCallToolResultType(raw.GetType()))
            return EnsureWireSafe(BridgeForeignCallToolResult(raw));

        if (IsForeignContentBlockType(raw.GetType()))
        {
            return EnsureWireSafe(new McpInvocationResponse
            {
                Content = [MapForeignContentBlock(raw)],
            });
        }

        if (raw is ContentBlock hostBlock)
        {
            return EnsureWireSafe(new McpInvocationResponse
            {
                Content = [MapHostContentBlock(hostBlock)],
            });
        }

        var element = JsonSerializer.SerializeToElement(raw, JsonOptions);
        if (IsCallToolResult(element))
        {
            var bridged = element.Deserialize<CallToolResult>(JsonOptions);
            if (bridged is not null)
                return EnsureWireSafe(MapHostCallToolResult(bridged));
        }

        return EnsureWireSafe(MapPlainResult(raw, element, outputSchema));
    }

    public static McpInvocationResponse EnsureWireSafe(McpInvocationResponse response) =>
        InvocationResponseEncoder.PrepareForWire(response);

    public static bool IsForeignCallToolResultType(Type type) =>
        string.Equals(type.Name, nameof(CallToolResult), StringComparison.Ordinal) &&
        !ReferenceEquals(type, typeof(CallToolResult));

    public static bool IsForeignContentBlockType(Type type)
    {
        if (typeof(ContentBlock).IsAssignableFrom(type))
            return false;

        return type.Name is nameof(TextContentBlock)
            or nameof(ImageContentBlock)
            or nameof(AudioContentBlock)
            or nameof(EmbeddedResourceBlock)
            or nameof(ResourceLinkBlock)
            or nameof(ContentBlock)
            or "ToolUseContentBlock"
            or "ToolResultContentBlock";
    }

    public static McpInvocationResponse BridgeForeignCallToolResult(object raw)
    {
        var content = new List<McpContent>();
        if (GetPropertyValue(raw, "Content") is IEnumerable items)
        {
            foreach (var item in items)
            {
                if (item is null)
                    continue;
                content.Add(MapForeignContentBlock(item));
            }
        }

        return new McpInvocationResponse
        {
            Content = content,
            StructuredContent = ReadJsonElement(GetPropertyValue(raw, "StructuredContent")),
            IsError = GetPropertyValue(raw, "IsError") as bool?,
            Meta = CloneJsonObject(GetPropertyValue(raw, "Meta")),
        };
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
        _ => new McpTextContent(JsonSerializer.Serialize(new
        {
            type = block.GetType().Name,
            note = "Unsupported host content block bridged as text.",
        })),
    };

    private static McpContent MapForeignContentBlock(object block)
    {
        var typeName = GetPropertyValue(block, "Type") as string
            ?? block.GetType().Name;

        return typeName switch
        {
            BlockTypes.Text or nameof(TextContentBlock) => new McpTextContent(
                GetPropertyValue(block, "Text") as string ?? string.Empty)
            {
                Meta = CloneJsonObject(GetPropertyValue(block, "Meta")),
            },
            BlockTypes.Image or nameof(ImageContentBlock) => MapForeignBinaryBlock(
                block,
                static (bytes, mime) => new McpImageContent(bytes, mime)),
            BlockTypes.Audio or nameof(AudioContentBlock) => MapForeignBinaryBlock(
                block,
                static (bytes, mime) => new McpAudioContent(bytes, mime)),
            BlockTypes.ResourceLink or nameof(ResourceLinkBlock) => new McpResourceLinkContent(
                GetPropertyValue(block, "Uri") as string ?? string.Empty,
                GetPropertyValue(block, "Name") as string ?? string.Empty,
                GetPropertyValue(block, "Title") as string,
                GetPropertyValue(block, "Description") as string,
                GetPropertyValue(block, "MimeType") as string,
                GetPropertyValue(block, "Size") as long?)
            {
                Meta = CloneJsonObject(GetPropertyValue(block, "Meta")),
            },
            BlockTypes.Resource or nameof(EmbeddedResourceBlock) => MapForeignEmbeddedResource(block),
            _ => new McpTextContent(JsonSerializer.Serialize(new
            {
                type = typeName,
                note = "Unsupported foreign content block bridged as text.",
            })),
        };
    }

    private static McpContent MapForeignBinaryBlock(object block, Func<byte[], string, McpContent> factory)
    {
        var mime = GetPropertyValue(block, "MimeType") as string ?? "application/octet-stream";
        var memory = ReadMemoryBytes(GetPropertyValue(block, "DecodedData"))
            ?? ReadMemoryBytes(GetPropertyValue(block, "Data"))
            ?? ReadOnlyMemory<byte>.Empty;
        var mapped = factory(memory.ToArray(), mime);
        if (mapped is McpImageContent image)
            return image with { Meta = CloneJsonObject(GetPropertyValue(block, "Meta")) };
        if (mapped is McpAudioContent audio)
            return audio with { Meta = CloneJsonObject(GetPropertyValue(block, "Meta")) };
        return mapped;
    }

    private static McpContent MapForeignEmbeddedResource(object block)
    {
        var resource = GetPropertyValue(block, "Resource");
        if (resource is null)
        {
            return new McpEmbeddedTextResourceContent(string.Empty, string.Empty, null)
            {
                Meta = CloneJsonObject(GetPropertyValue(block, "Meta")),
            };
        }

        var uri = GetPropertyValue(resource, "Uri") as string ?? string.Empty;
        var mime = GetPropertyValue(resource, "MimeType") as string;
        var text = GetPropertyValue(resource, "Text") as string;
        if (text is not null || resource.GetType().Name.Contains("Text", StringComparison.Ordinal))
        {
            return new McpEmbeddedTextResourceContent(uri, text ?? string.Empty, mime)
            {
                Meta = CloneJsonObject(GetPropertyValue(block, "Meta")),
                ResourceMeta = CloneJsonObject(GetPropertyValue(resource, "Meta")),
            };
        }

        var blob = ReadMemoryBytes(GetPropertyValue(resource, "DecodedData"))
            ?? ReadMemoryBytes(GetPropertyValue(resource, "Data"))
            ?? ReadOnlyMemory<byte>.Empty;
        return new McpEmbeddedBlobResourceContent(uri, blob.ToArray(), mime)
        {
            Meta = CloneJsonObject(GetPropertyValue(block, "Meta")),
            ResourceMeta = CloneJsonObject(GetPropertyValue(resource, "Meta")),
        };
    }

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

    private static bool IsCallToolResult(JsonElement element) =>
        element.ValueKind is JsonValueKind.Object &&
        (element.TryGetProperty(ToolResultKeys.Content, out _) ||
         element.TryGetProperty(ToolResultKeys.ContentPascal, out _));

    private static JsonElement? CreateStructuredContent(JsonElement? outputSchema, JsonElement naturalValue)
    {
        if (outputSchema is null)
            return null;

        return naturalValue.Clone();
    }

    private static object? GetPropertyValue(object target, string name) =>
        target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
            ?.GetValue(target);

    private static JsonElement? ReadJsonElement(object? value)
    {
        if (value is null)
            return null;
        if (value is JsonElement element)
            return element.Clone();
        return JsonSerializer.SerializeToElement(value, value.GetType(), JsonOptions);
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

    private static ReadOnlyMemory<byte>? ReadMemoryBytes(object? value) =>
        value switch
        {
            null => null,
            ReadOnlyMemory<byte> memory => memory,
            Memory<byte> memory => memory,
            byte[] bytes => bytes,
            _ => null,
        };
}

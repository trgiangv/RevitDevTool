using System.Text.Json;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Server.Hosting;

/// <summary>
/// Binary-safe serialization for MCP call-monitor log lines at protocol filters.
/// </summary>
internal static class McpLogPayload
{

    public static string SerializeArgs(IEnumerable<KeyValuePair<string, JsonElement>>? arguments)
    {
        if (arguments is null)
            return "{}";

        var dict = arguments as IReadOnlyDictionary<string, JsonElement>
            ?? arguments.ToDictionary(static pair => pair.Key, static pair => pair.Value);
        return dict.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(dict, McpLogJsonContext.Default.DictionaryStringJsonElement);
    }

    public static string SerializeCallToolResult(CallToolResult result)
    {
        if (result.StructuredContent is { } structured && !HasBinaryCallToolContent(result))
            return structured.GetRawText();

        var payload = new LogCallToolResult(
            result.IsError,
            result.Content.Select(RedactContentBlock).ToArray(),
            result.StructuredContent);
        return JsonSerializer.Serialize(payload, McpLogJsonContext.Default.LogCallToolResult);
    }

    public static string SerializeReadResourceResult(ReadResourceResult result)
    {
        var payload = new LogReadResourceResult(result.Contents.Select(RedactResourceContent).ToArray());
        return JsonSerializer.Serialize(payload, McpLogJsonContext.Default.LogReadResourceResult);
    }

    private static LogResourceContent RedactResourceContent(ResourceContents item) => item switch
    {
        BlobResourceContents blob => new LogResourceContent("blob", blob.MimeType, blob.Uri, blob.DecodedData.Length),
        TextResourceContents text => new LogResourceContent("text", text.MimeType, text.Uri, Text: text.Text),
        _ => new LogResourceContent(item.GetType().Name)
    };

    private static LogContentBlock RedactContentBlock(ContentBlock block) => block switch
    {
        ImageContentBlock image => new LogContentBlock(image.Type, image.MimeType, image.DecodedData.Length),
        AudioContentBlock audio => new LogContentBlock(audio.Type, audio.MimeType, audio.DecodedData.Length),
        TextContentBlock text when TryParseResourceContents(text.Text, out var resource) && HasBinaryContents(resource) =>
            new LogContentBlock(text.Type, ReadResource: resource),
        TextContentBlock text => new LogContentBlock(text.Type, Text: text.Text),
        _ => new LogContentBlock(block.Type)
    };

    private static bool HasBinaryCallToolContent(CallToolResult result) =>
        result.Content.Any(block => block is ImageContentBlock or AudioContentBlock)
        || result.Content.OfType<TextContentBlock>().Any(block =>
            TryParseResourceContents(block.Text, out var resource) && HasBinaryContents(resource));

    private static bool HasBinaryContents(LogReadResourceResult resource) =>
        resource.Contents.Any(node => node.Type == "blob");

    private static bool TryParseResourceContents(string? text, out LogReadResourceResult resource)
    {
        resource = new LogReadResourceResult([]);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            var parsed = JsonSerializer.Deserialize(text, ToolHelpers.ProtocolOptions.GetTypeInfo(typeof(ReadResourceResult))!);
            if (parsed is not ReadResourceResult readResult || readResult.Contents.Count == 0)
                return false;
            resource = new LogReadResourceResult(readResult.Contents.Select(RedactResourceContent).ToArray());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

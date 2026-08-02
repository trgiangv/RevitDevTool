using System.Text.Json;
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

        var map = arguments as Dictionary<string, JsonElement>
                  ?? new Dictionary<string, JsonElement>(
                      arguments as IDictionary<string, JsonElement>
                      ?? arguments.ToDictionary(kv => kv.Key, kv => kv.Value));

        return map.Count == 0 ? "{}" : Serialize(map);
    }

    public static string SerializeCallToolResult(CallToolResult result)
    {
        if (result.StructuredContent is { } summary && !HasBinaryCallToolContent(result))
            return summary.GetRawText();

        if (!HasBinaryCallToolContent(result))
            return Serialize(result);

        var content = new List<object>(result.Content.Count);
        foreach (var block in result.Content)
        {
            content.Add(block switch
            {
                ImageContentBlock image => new { type = image.Type, mimeType = image.MimeType, length = image.DecodedData.Length },
                AudioContentBlock audio => new { type = audio.Type, mimeType = audio.MimeType, length = audio.DecodedData.Length },
                TextContentBlock text when TryParseReadResourceResult(text.Text, out var resource) => new { type = text.Type, readResource = Deserialize<JsonElement>(SerializeReadResourceResult(resource))! },
                TextContentBlock text => new { type = text.Type, text = text.Text },
                _ => new { type = block.Type }
            });
        }

        return Serialize(new { isError = result.IsError, structuredContent = result.StructuredContent, content });
    }

    public static string SerializeReadResourceResult(ReadResourceResult result)
    {
        if (!HasBinaryResourceContent(result))
            return Serialize(result);

        var contents = new List<object>(result.Contents.Count);
        contents.AddRange(result.Contents.Select(item => (object)(item switch
        {
            BlobResourceContents blob => new { type = "blob", mimeType = blob.MimeType, uri = blob.Uri, length = blob.DecodedData.Length },
            TextResourceContents text => new { type = "text", mimeType = text.MimeType, uri = text.Uri, text = text.Text },
            _ => new { type = item.GetType().Name }
        })));

        return Serialize(new { contents });
    }

    private static bool HasBinaryCallToolContent(CallToolResult result) =>
        result.Content.Any(block => block is ImageContentBlock or AudioContentBlock) || HasTextWrappedBinaryResource(result);

    private static bool HasTextWrappedBinaryResource(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().Any(block =>
            TryParseReadResourceResult(block.Text, out var resource) && HasBinaryResourceContent(resource));

    private static bool TryParseReadResourceResult(string? text, out ReadResourceResult resource)
    {
        resource = new ReadResourceResult();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            var parsed = Deserialize<ReadResourceResult>(text!);
            if (parsed is null || parsed.Contents.Count == 0)
                return false;

            resource = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasBinaryResourceContent(ReadResourceResult result) =>
        result.Contents.OfType<BlobResourceContents>().Any();

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, McpJsonUtilities.DefaultOptions);

    private static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, McpJsonUtilities.DefaultOptions);
}

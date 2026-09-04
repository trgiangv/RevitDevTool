using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevTools.Mcp.Server.Hosting;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(LogCallToolResult))]
[JsonSerializable(typeof(LogContentBlock[]))]
[JsonSerializable(typeof(LogReadResourceResult))]
[JsonSerializable(typeof(LogResourceContent[]))]
internal sealed partial class McpLogJsonContext : JsonSerializerContext;

internal sealed record LogCallToolResult(
    bool? IsError,
    LogContentBlock[] Content,
    JsonElement? StructuredContent);

internal sealed record LogContentBlock(string Type, string? MimeType = null, int? Length = null, string? Text = null, LogReadResourceResult? ReadResource = null);

internal sealed record LogReadResourceResult(LogResourceContent[] Contents);

internal sealed record LogResourceContent(string Type, string? MimeType = null, string? Uri = null, int? Length = null, string? Text = null);

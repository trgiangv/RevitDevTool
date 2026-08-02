using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Utils;

/// <summary>Shared MCP tool result helpers and protocol JSON (single entry for <see cref="McpJsonUtilities.DefaultOptions"/>).</summary>
public static class ToolHelpers
{
    private static readonly JsonSerializerOptions Options = McpJsonUtilities.DefaultOptions;

    public static CallToolResult ErrorResult(string message) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = message }] };

    public static CallToolResult Result(string text) =>
        new() { Content = [new TextContentBlock { Text = text }] };

    public static CallToolResult Result<T>(T value) =>
        Result(Serialize(value));

    public static CallToolResult ImageResult(ReadOnlyMemory<byte> data, string mimeType) =>
        new() { Content = [ImageContentBlock.FromBytes(data, mimeType)] };

    /// <summary>
    /// Serializes <paramref name="value"/> using its runtime type so derived
    /// FileInfo / MCP payload fields are not dropped when the declared type is a base class.
    /// </summary>
    public static string Serialize<T>(T value) =>
        value is null
            ? JsonSerializer.Serialize(value, Options)
            : JsonSerializer.Serialize(value, value.GetType(), Options);

    public static string Serialize(object value, Type type) =>
        JsonSerializer.Serialize(value, type, Options);

    public static JsonElement ToElement<T>(T value) =>
        value is null
            ? JsonSerializer.SerializeToElement(value, Options)
            : JsonSerializer.SerializeToElement(value, value.GetType(), Options);

    internal static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options);
}

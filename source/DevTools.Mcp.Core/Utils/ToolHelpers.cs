using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Utils;

/// <summary>Shared MCP tool result helpers and protocol JSON (single entry for <see cref="McpJsonUtilities.DefaultOptions"/>).</summary>
public static class ToolHelpers
{
    public static JsonSerializerOptions ProtocolOptions => McpJsonUtilities.DefaultOptions;

    /// <summary>
    /// Runtime-object options for values that may come from another load context.
    /// Keeps the MCP converters while using camelCase and reflection metadata.
    /// </summary>
    public static JsonSerializerOptions RuntimeJsonOptions { get; } = CreateRuntimeJsonOptions();

    private static JsonSerializerOptions CreateRuntimeJsonOptions() => new(ProtocolOptions)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static CallToolResult ErrorResult(string message) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = message }] };

    public static CallToolResult ErrorResult<T>(T value) => ErrorResult(Serialize(value));

    public static CallToolResult Result(string text) =>
        new() { Content = [new TextContentBlock { Text = text }] };

    public static CallToolResult Result<T>(T value) => Result(Serialize(value));

    public static CallToolResult Result<T>(T value, JsonTypeInfo<T> typeInfo) =>
        Result(JsonSerializer.Serialize(value, typeInfo));

    public static CallToolResult ImageResult(ReadOnlyMemory<byte> data, string mimeType) =>
        new() { Content = [ImageContentBlock.FromBytes(data, mimeType)] };

    /// <summary>
    /// Serializes <paramref name="value"/> using its runtime type so derived
    /// FileInfo / MCP payload fields are not dropped when the declared type is a base class.
    /// Typed DTO callers should pass <see cref="JsonTypeInfo{T}"/> to <see cref="Result{T}(T, JsonTypeInfo{T})"/> instead.
    /// </summary>
    public static string Serialize<T>(T value) =>
        value is null
            ? JsonSerializer.Serialize(value, ProtocolOptions)
            : JsonSerializer.Serialize(value, value.GetType(), ProtocolOptions);

    public static string Serialize(object value, Type type) =>
        JsonSerializer.Serialize(value, type, ProtocolOptions);

    public static JsonElement ToElement<T>(T value) =>
        value is null
            ? JsonSerializer.SerializeToElement(value, ProtocolOptions)
            : JsonSerializer.SerializeToElement(value, value.GetType(), ProtocolOptions);

    internal static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, ProtocolOptions);
}

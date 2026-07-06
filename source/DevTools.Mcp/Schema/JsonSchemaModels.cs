using System.Text.Json;
using System.Text.Json.Serialization;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Mcp.Schema;

/// <summary>
/// Typed model for a JSON Schema "object" with named properties.
/// Supports both serialization (schema builder) and deserialization (UI, schema mutation).
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class JsonSchemaObject
{
    [JsonPropertyName(IpcPropertyNames.Type)]
    public string Type { get; init; } = JsonSchemaTypeNames.Object;

    [JsonPropertyName(McpPropertyNames.Properties)]
    public Dictionary<string, JsonSchemaProperty>? Properties { get; set; }

    [JsonPropertyName(McpPropertyNames.Required)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Required { get; set; }

    /// <summary>Serialize to a <see cref="JsonElement"/> suitable for <c>Tool.InputSchema</c>.</summary>
    public JsonElement ToElement() => JsonSerializer.SerializeToElement(this);

    /// <summary>Try to deserialize a raw JSON Schema string into a typed model.</summary>
    public static JsonSchemaObject? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<JsonSchemaObject>(json!);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// A single property within a JSON Schema "properties" block.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class JsonSchemaProperty
{
    [JsonPropertyName(IpcPropertyNames.Type)]
    public string? Type { get; init; }

    [JsonPropertyName(McpPropertyNames.Title)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [JsonPropertyName(McpPropertyNames.Description)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

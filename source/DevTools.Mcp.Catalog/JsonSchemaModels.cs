using System.Text.Json;
using System.Text.Json.Serialization;
using DevTools.Mcp.Core.Protocol;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Mcp.Catalog;

/// <summary>
/// Typed model for parsing/reading a JSON Schema "object" (UI, mutation).
/// Prefer SDK <c>McpServerTool.Create</c> when constructing tool schemas.
/// This model is for parsing/reading schemas (UI).
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class JsonSchemaObject
{
    [JsonPropertyName(McpSpecKeys.JsonSchema.Type)]
    public string Type { get; init; } = McpSpecKeys.JsonSchema.Types.Object;

    [JsonPropertyName(McpSpecKeys.JsonSchema.Properties)]
    public Dictionary<string, JsonSchemaProperty>? Properties { get; set; }

    [JsonPropertyName(McpSpecKeys.JsonSchema.Required)]
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

/// <summary>A single property within a JSON Schema "properties" block.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class JsonSchemaProperty
{
    [JsonPropertyName(McpSpecKeys.JsonSchema.Type)]
    public string? Type { get; init; }

    [JsonPropertyName(McpSpecKeys.JsonSchema.Title)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [JsonPropertyName(McpSpecKeys.JsonSchema.Description)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

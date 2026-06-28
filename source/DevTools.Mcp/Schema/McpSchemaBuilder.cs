using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevTools.Mcp.Schema;

/// <summary>
/// Produces typed JsonElement values for MCP tool InputSchema definitions,
/// replacing anonymous object serialization.
/// </summary>
public static class McpSchemaBuilder
{
    public static JsonElement EmptyObject() =>
        JsonSerializer.SerializeToElement(new EmptyObjectSchema());

    public static JsonElement Object(SchemaProperty[] properties, string[]? required) =>
        JsonSerializer.SerializeToElement(new ObjectSchema(properties, required));

    public static SchemaProperty String(string name, string description) =>
        new(name, JsonSchemaTypeNames.String, description);

    public static SchemaProperty Integer(string name, string description) =>
        new(name, JsonSchemaTypeNames.Integer, description);

    public static SchemaProperty ObjectProp(string name, string description) =>
        new(name, JsonSchemaTypeNames.Object, description);
}

public sealed record SchemaProperty(string Name, string Type, string Description);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal sealed class EmptyObjectSchema
{
    [JsonPropertyName("type")]
    public string Type => JsonSchemaTypeNames.Object;

    [JsonPropertyName("properties")]
    public EmptyProperties Properties { get; } = new();
}

internal sealed class EmptyProperties;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal sealed class ObjectSchema
{
    [JsonPropertyName("type")]
    public string Type => JsonSchemaTypeNames.Object;

    [JsonPropertyName("properties")]
    public Dictionary<string, PropertySchema> Properties { get; }

    [JsonPropertyName("required")]
    public string[]? Required { get; }

    public ObjectSchema(SchemaProperty[] props, string[]? required)
    {
        Properties = props.ToDictionary(p => p.Name, p => new PropertySchema(p.Type, p.Description));
        Required = required;
    }
}

[UsedImplicitly]
[method: JsonConstructor]
internal sealed record PropertySchema(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string Description);

using System.Text.Json;

namespace DevTools.Mcp.Schema;

/// <summary>
/// Produces typed JsonElement values for MCP tool InputSchema definitions.
/// Backed by <see cref="JsonSchemaObject"/> / <see cref="JsonSchemaProperty"/>.
/// </summary>
public static class McpSchemaBuilder
{
    public static JsonElement EmptyObject() =>
        new JsonSchemaObject { Properties = new Dictionary<string, JsonSchemaProperty>() }.ToElement();

    public static JsonElement Object(SchemaProperty[] properties, string[]? required)
    {
        var schema = new JsonSchemaObject
        {
            Properties = properties.ToDictionary(
                p => p.Name,
                p => BuildProperty(p)),
            Required = required
        };
        return schema.ToElement();
    }

    private static JsonSchemaProperty BuildProperty(SchemaProperty p)
    {
        var prop = new JsonSchemaProperty { Type = p.Type, Description = p.Description };
        if (p.EnumValues is { Length: > 0 })
        {
            prop.ExtensionData ??= new Dictionary<string, JsonElement>();
            prop.ExtensionData["enum"] = JsonSerializer.SerializeToElement(p.EnumValues);
        }
        return prop;
    }

    public static SchemaProperty String(string name, string description) =>
        new(name, JsonSchemaTypeNames.String, description);

    public static SchemaProperty Enum(string name, string description, string[] values) =>
        new(name, JsonSchemaTypeNames.String, description, values);

    public static SchemaProperty Integer(string name, string description) =>
        new(name, JsonSchemaTypeNames.Integer, description);

    public static SchemaProperty ObjectProp(string name, string description) =>
        new(name, JsonSchemaTypeNames.Object, description);
}

public sealed record SchemaProperty(string Name, string Type, string Description, string[]? EnumValues = null);

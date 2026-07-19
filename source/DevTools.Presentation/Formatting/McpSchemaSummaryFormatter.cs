using System.Text.Json;

namespace DevTools.Presentation.Formatting;

internal static class McpSchemaSummaryFormatter
{
    public static string Format(JsonElement inputSchema)
    {
        if (inputSchema.ValueKind != JsonValueKind.Object ||
            !inputSchema.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
            return string.Empty;

        var lines = new List<string>();
        foreach (var property in properties.EnumerateObject())
        {
            var title = ReadOptionalString(property.Value, "title") ?? property.Name;
            var description = ReadOptionalString(property.Value, "description");
            var suffix = string.IsNullOrWhiteSpace(description) ? string.Empty : $" — {description}";
            lines.Add($"- {property.Name}: {title} ({FormatType(property.Value)}){suffix}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatType(JsonElement schema)
    {
        var types = new List<string>();
        CollectTypes(schema, types);
        return types.Count == 0 ? "any" : string.Join(" | ", types);
    }

    private static void CollectTypes(JsonElement schema, List<string> types)
    {
        if (schema.ValueKind != JsonValueKind.Object)
            return;

        if (schema.TryGetProperty("type", out var type))
        {
            switch (type.ValueKind)
            {
                case JsonValueKind.String:
                    Add(type.GetString(), types);
                    break;
                case JsonValueKind.Array:
                {
                    foreach (var item in type.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String)
                            Add(item.GetString(), types);
                    break;
                }
            }
        }

        CollectAlternatives(schema, "anyOf", types);
        CollectAlternatives(schema, "oneOf", types);
    }

    private static void CollectAlternatives(JsonElement schema, string keyword, List<string> types)
    {
        if (!schema.TryGetProperty(keyword, out var alternatives) || alternatives.ValueKind != JsonValueKind.Array)
            return;

        foreach (var alternative in alternatives.EnumerateArray())
            CollectTypes(alternative, types);
    }

    private static void Add(string? type, List<string> types)
    {
        if (!string.IsNullOrWhiteSpace(type) && !types.Contains(type!, StringComparer.Ordinal))
            types.Add(type!);
    }

    private static string? ReadOptionalString(JsonElement owner, string name)
    {
        if (owner.ValueKind != JsonValueKind.Object ||
            !owner.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String)
            return null;

        return value.GetString();
    }
}

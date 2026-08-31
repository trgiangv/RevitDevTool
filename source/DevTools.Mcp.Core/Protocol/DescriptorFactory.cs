using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Protocol;

/// <summary>Helpers for building and normalizing SDK catalog protocol types.</summary>
public static class DescriptorFactory
{
    private static readonly JsonElement DefaultObjectSchema =
        JsonSerializer.SerializeToElement(new { type = "object" });

    /// <summary>
    /// Returns <paramref name="schema"/> when it is a valid MCP tool input schema;
    /// otherwise <c>{"type":"object"}</c>.
    /// </summary>
    public static JsonElement CoerceInputSchema(JsonElement schema)
    {
        if (IsValidMcpToolSchema(schema))
            return schema;

        return DefaultObjectSchema;
    }

    /// <summary>Ensures <see cref="Tool.InputSchema"/> is valid after parser or wire deserialization.</summary>
    public static Tool NormalizeTool(Tool tool)
    {
        tool.InputSchema = CoerceInputSchema(tool.InputSchema);
        return tool;
    }

    public static ToolAnnotations? BuildToolAnnotations(
        string? title,
        bool? readOnly = null,
        bool? destructive = null,
        bool? idempotent = null,
        bool? openWorld = null)
    {
        if (string.IsNullOrWhiteSpace(title)
            && readOnly is null
            && destructive is null
            && idempotent is null
            && openWorld is null)
        {
            return null;
        }

        return new ToolAnnotations
        {
            DestructiveHint = destructive,
            IdempotentHint = idempotent,
            OpenWorldHint = openWorld,
            ReadOnlyHint = readOnly,
            Title = title,
        };
    }

    public static IList<Icon>? ParseIcons(string? iconSource)
    {
        if (string.IsNullOrWhiteSpace(iconSource))
            return null;

        return [new Icon { Source = iconSource!.Trim() }];
    }

    private static bool IsValidMcpToolSchema(JsonElement element)
    {
        if (element.ValueKind is not JsonValueKind.Object)
            return false;

        foreach (var property in element.EnumerateObject())
        {
            if (!property.NameEquals("type"))
                continue;

            return property.Value.ValueKind is JsonValueKind.String
                   && string.Equals(property.Value.GetString(), "object", StringComparison.Ordinal);
        }

        return false;
    }
}

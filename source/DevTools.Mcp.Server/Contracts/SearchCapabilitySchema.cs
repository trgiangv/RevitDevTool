using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Server.Contracts;

/// <summary>Builds <c>requiredArgs</c> and <c>argsHint</c> for <see cref="SearchCapabilityItem"/>.</summary>
internal static class SearchCapabilitySchemaHints
{
    private const string InputSchemaPropertiesKey = "properties";
    private const string InputSchemaRequiredKey = "required";
    private const string TemplateParameterPattern = "\\{([^}]+)\\}";

    private static readonly Regex TemplateParameterRegex = new(TemplateParameterPattern, RegexOptions.CultureInvariant);

    public static string[]? ExtractArgsHint(JsonElement? schema) =>
        GetSchemaNames(schema, InputSchemaPropertiesKey, SearchDynamicLimits.MaximumArgsHintCount);

    public static string[]? ExtractRequiredArgs(JsonElement? schema) =>
        GetSchemaNames(schema, InputSchemaRequiredKey, int.MaxValue);

    public static string[]? ExtractTemplateArgsHint(ResourceTemplate? template)
    {
        if (template is null)
            return null;

        var matches = TemplateParameterRegex.Matches(template.UriTemplate);
        if (matches.Count == 0)
            return null;

        var names = new List<string>(Math.Min(matches.Count, SearchDynamicLimits.MaximumArgsHintCount));
        for (var i = 0; i < matches.Count && names.Count < SearchDynamicLimits.MaximumArgsHintCount; i++)
            names.Add(matches[i].Groups[1].Value);

        return names.Count == 0 ? null : names.ToArray();
    }

    private static string[]? GetSchemaNames(JsonElement? schema, string property, int maximum)
    {
        if (schema is not { ValueKind: JsonValueKind.Object } root || !root.TryGetProperty(property, out var values))
            return null;

        var names = property == InputSchemaPropertiesKey && values.ValueKind == JsonValueKind.Object
            ? values.EnumerateObject().Select(value => value.Name).Take(maximum).ToArray()
            : values.ValueKind == JsonValueKind.Array
                ? values.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString()!)
                    .Take(maximum)
                    .ToArray()
                : [];

        return names.Length == 0 ? null : names;
    }
}

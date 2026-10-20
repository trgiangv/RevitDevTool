using System.Globalization;
using System.Text.Json;
using DevTools.Mcp.Core.Protocol;
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

namespace DevTools.Daemon.Mcp.Search;

internal static class SchemaIndexer
{
    private const int MaximumDepth = 64;

    public static SchemaLengths Index(
        JsonElement inputSchema,
        JsonElement? outputSchema,
        Dictionary<string, Scoring.FieldFrequency> frequencies)
    {
        var input = new SchemaDocument(inputSchema, frequencies);
        input.Visit(inputSchema, SchemaKind.Input);

        var schemaLength = input.SchemaLength;
        if (outputSchema is { } output)
        {
            var outputDocument = new SchemaDocument(output, frequencies);
            outputDocument.Visit(output, SchemaKind.Output);
            schemaLength += outputDocument.SchemaLength;
        }

        return new SchemaLengths(
            input.ParameterLength,
            schemaLength,
            input.SupportsArbitraryParameterFilter);
    }

    internal readonly record struct SchemaLengths(
        int ParameterLength,
        int SchemaLength,
        bool SupportsArbitraryParameterFilter);

    private enum SchemaKind
    {
        Input,
        Output,
    }

    private sealed class SchemaDocument(JsonElement rootSchema, Dictionary<string, Scoring.FieldFrequency> frequencies)
    {
        private readonly HashSet<string> _visitedReferences = new(StringComparer.Ordinal);

        public int ParameterLength { get; private set; }
        public int SchemaLength { get; private set; }
        public bool SupportsArbitraryParameterFilter { get; private set; }

        public void Visit(JsonElement schema, SchemaKind kind, int depth = 0)
        {
            if (schema.ValueKind != JsonValueKind.Object || depth >= MaximumDepth)
                return;

            AddMetadata(schema);
            VisitReference(schema, kind, depth + 1);
            VisitProperties(schema, kind, depth + 1);
            VisitChildren(schema, kind, depth + 1);
        }

        private void AddMetadata(JsonElement schema)
        {
            AddString(schema, "title");
            AddString(schema, "description");
            AddString(schema, "type");
            AddString(schema, "const");
            AddString(schema, "enum");

            if (!schema.TryGetProperty("required", out var required) || required.ValueKind != JsonValueKind.Array)
                return;

            foreach (var entry in required.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String)
                    SchemaLength += AddTokens(entry.GetString(), Scoring.SearchField.Schema);
            }
        }

        private void VisitReference(JsonElement schema, SchemaKind kind, int depth)
        {
            if (!schema.TryGetProperty("$ref", out var reference)
                || reference.ValueKind != JsonValueKind.String)
                return;

            var path = reference.GetString();
            if (string.IsNullOrWhiteSpace(path) || !_visitedReferences.Add(path))
                return;
            if (!TryResolveLocalReference(path, out var referencedSchema))
                return;

            Visit(referencedSchema, kind, depth);
        }

        private bool TryResolveLocalReference(string reference, out JsonElement resolvedSchema)
        {
            resolvedSchema = rootSchema;
            if (reference == "#")
                return true;
            if (!reference.StartsWith("#/", StringComparison.Ordinal))
                return false;

            foreach (var rawSegment in reference[2..].Split('/'))
            {
                var segment = UnescapePointerSegment(rawSegment);
                if (!TryGetChild(resolvedSchema, segment, out var child))
                {
                    resolvedSchema = default;
                    return false;
                }

                resolvedSchema = child;
            }

            return true;
        }

        private static bool TryGetChild(JsonElement element, string segment, out JsonElement child)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    return element.TryGetProperty(segment, out child);
                case JsonValueKind.Array:
                    if (!int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                        break;
                    if (index >= element.GetArrayLength())
                        break;

                    child = element[index];
                    return true;
            }

            child = default;
            return false;
        }

        private static string UnescapePointerSegment(string segment) =>
            segment.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);

        private void VisitProperties(JsonElement schema, SchemaKind kind, int depth)
        {
            if (!schema.TryGetProperty(McpSpecKeys.JsonSchema.Properties, out var properties)
                || properties.ValueKind != JsonValueKind.Object)
                return;

            var field = kind == SchemaKind.Input
                ? Scoring.SearchField.Parameters
                : Scoring.SearchField.Schema;
            var hasParameterName = false;
            var hasValue = false;
            foreach (var property in properties.EnumerateObject())
            {
                var propertyTokens = Tokenizer.Tokenize(property.Name);
                hasParameterName |= propertyTokens.Contains("parameter", StringComparer.Ordinal);
                hasValue |= propertyTokens.Contains("value", StringComparer.Ordinal);

                AddPropertyName(property.Name, field);
                Visit(property.Value, kind, depth);
            }

            if (kind == SchemaKind.Input && hasParameterName && hasValue)
                SupportsArbitraryParameterFilter = true;
        }

        private void AddPropertyName(string name, Scoring.SearchField field)
        {
            var length = AddTokens(name, field);
            if (field == Scoring.SearchField.Parameters)
                ParameterLength += length;
            else
                SchemaLength += length;
        }

        private void VisitChildren(JsonElement schema, SchemaKind kind, int depth)
        {
            foreach (var property in schema.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "properties":
                    case "required":
                    case "$ref":
                    case "$defs":
                    case "definitions":
                        break;
                    case "items":
                    case "additionalProperties":
                    case "contains":
                    case "not":
                    case "if":
                    case "then":
                    case "else":
                        Visit(property.Value, kind, depth);
                        break;
                    case "allOf":
                    case "anyOf":
                    case "oneOf":
                        VisitSchemaAlternatives(property.Value, kind, depth);
                        break;
                }
            }
        }

        private void VisitSchemaAlternatives(JsonElement alternatives, SchemaKind kind, int depth)
        {
            if (alternatives.ValueKind == JsonValueKind.Array)
            {
                foreach (var alternative in alternatives.EnumerateArray())
                    Visit(alternative, kind, depth);
            }
            else
            {
                Visit(alternatives, kind, depth);
            }
        }

        private void AddString(JsonElement schema, string propertyName)
        {
            if (!schema.TryGetProperty(propertyName, out var value))
                return;

            if (value.ValueKind == JsonValueKind.String)
            {
                SchemaLength += AddTokens(value.GetString(), Scoring.SearchField.Schema);
                return;
            }

            if (value.ValueKind != JsonValueKind.Array)
                return;

            foreach (var entry in value.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String)
                    SchemaLength += AddTokens(entry.GetString(), Scoring.SearchField.Schema);
            }
        }

        private int AddTokens(string? text, Scoring.SearchField field) =>
            Scoring.AddTokens(frequencies, Tokenizer.Tokenize(text), field);
    }
}

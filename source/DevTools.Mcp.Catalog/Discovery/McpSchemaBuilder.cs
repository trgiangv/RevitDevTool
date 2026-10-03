using DevTools.Mcp.Core.Protocol;
using System.Reflection;
using System.Text.Json.Nodes;

namespace DevTools.Mcp.Discovery;

using JsonTypes = McpSpecKeys.JsonSchema.Types;

/// <summary>
/// Shared CLR → JSON Schema type mapping for discovery paths that cannot use
/// SDK <c>McpServerTool.Create</c> (MetadataLoadContext assembly parsing).
/// Daemon and host built-in tools should prefer SDK Create for schema + invoke.
/// </summary>
public static class McpSchemaBuilder
{
    private const string NullableGenericFullName = "System.Nullable`1";
    private const string TaskGenericFullName = "System.Threading.Tasks.Task`1";
    private const string ValueTaskGenericFullName = "System.Threading.Tasks.ValueTask`1";
    private const string JsonIgnoreAttributeFullName = "System.Text.Json.Serialization.JsonIgnoreAttribute";
    private const string DescriptionAttributeFullName = "System.ComponentModel.DescriptionAttribute";

    /// <summary>
    /// Maps a CLR type (including MetadataLoadContext types matched by FullName)
    /// to a JSON Schema primitive type name.
    /// </summary>
    private static string FromClrType(Type type)
    {
        type = UnwrapNullable(type);
        return Type.GetTypeCode(type) switch
        {
            TypeCode.String or TypeCode.Char => JsonTypes.String,
            TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32
                or TypeCode.Int64 or TypeCode.UInt64 => JsonTypes.Integer,
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => JsonTypes.Number,
            TypeCode.Boolean => JsonTypes.Boolean,
            _ => type.IsEnum ? JsonTypes.String : JsonTypes.Object
        };
    }

    /// <summary>Builds the JSON Schema subset expressible from metadata attributes.</summary>
    public static JsonObject BuildSchema(Type type, string? description = null)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var schema = BuildSchemaNode(UnwrapReturnType(UnwrapNullable(type)), visited);
        if (!string.IsNullOrWhiteSpace(description))
            schema[McpSpecKeys.JsonSchema.Description] = description;

        return schema;
    }

    private static JsonObject BuildSchemaNode(Type type, HashSet<string> visited)
    {
        var typeKey = type.FullName ?? type.Name;
        if (!visited.Add(typeKey))
            return new JsonObject { [McpSpecKeys.JsonSchema.Type] = JsonTypes.Object };

        if (type.IsEnum)
            return BuildEnumSchema(type);

        if (TryGetCollectionElement(type, out var elementType))
            return BuildCollectionSchema(elementType, visited);

        if (TryGetDictionaryValue(type, out var valueType))
            return BuildDictionarySchema(valueType, visited);

        var primitive = FromClrType(type);
        if (primitive is JsonTypes.String or JsonTypes.Integer or JsonTypes.Number or JsonTypes.Boolean)
            return new JsonObject { [McpSpecKeys.JsonSchema.Type] = primitive };

        if (type == typeof(object))
            return new JsonObject { [McpSpecKeys.JsonSchema.Type] = JsonTypes.Object };

        return BuildObjectSchema(type, visited);
    }

    private static JsonObject BuildEnumSchema(Type type)
    {
        var values = new JsonArray();
        foreach (var value in Enum.GetNames(type))
            values.Add(value);
        return new JsonObject { [McpSpecKeys.JsonSchema.Type] = JsonTypes.String, ["enum"] = values };
    }

    private static JsonObject BuildCollectionSchema(Type elementType, HashSet<string> visited) =>
        new()
        {
            [McpSpecKeys.JsonSchema.Type] = JsonTypes.Array,
            [McpSpecKeys.JsonSchema.Items] = BuildSchemaNode(UnwrapNullable(elementType), visited),
        };

    private static JsonObject BuildDictionarySchema(Type valueType, HashSet<string> visited) =>
        new()
        {
            [McpSpecKeys.JsonSchema.Type] = JsonTypes.Object,
            [McpSpecKeys.JsonSchema.AdditionalProperties] = BuildSchemaNode(UnwrapNullable(valueType), visited),
        };

    private static JsonObject BuildObjectSchema(Type type, HashSet<string> visited)
    {
        var properties = new JsonObject();
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0 || IsJsonIgnored(property))
                continue;
            var propSchema = BuildSchemaNode(UnwrapNullable(property.PropertyType), visited);
            var propDescription = ReadDescription(property.CustomAttributes);
            if (!string.IsNullOrWhiteSpace(propDescription))
                propSchema[McpSpecKeys.JsonSchema.Description] = propDescription;
            properties[ToCamel(property.Name)] = propSchema;
        }

        return new JsonObject
        {
            [McpSpecKeys.JsonSchema.Type] = JsonTypes.Object,
            [McpSpecKeys.JsonSchema.Properties] = properties,
        };
    }

    private static Type UnwrapNullable(Type type)
    {
        while (true)
        {
            if (!type.IsGenericType || type.GetGenericArguments().Length == 0)
                return type;

            var genericDefFullName = type.GetGenericTypeDefinition().FullName;
            if (!string.Equals(genericDefFullName, NullableGenericFullName, StringComparison.Ordinal)
                && !string.Equals(genericDefFullName, typeof(Nullable<>).FullName, StringComparison.Ordinal))
                return type;

            type = type.GetGenericArguments()[0];
        }
    }

    private static Type UnwrapReturnType(Type type)
    {
        if (!type.IsGenericType)
            return type;
        var genericName = type.GetGenericTypeDefinition().FullName;
        return genericName is TaskGenericFullName or ValueTaskGenericFullName
            ? UnwrapReturnType(type.GetGenericArguments()[0])
            : type;
    }

    private static bool TryGetCollectionElement(Type type, out Type elementType)
    {
        elementType = null!;
        if (type == typeof(string) || type == typeof(byte[]))
            return false;
        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }
        if (!type.IsGenericType)
            return false;
        var definition = type.GetGenericTypeDefinition().FullName;
        if (definition is "System.Collections.Generic.IEnumerable`1"
            or "System.Collections.Generic.ICollection`1"
            or "System.Collections.Generic.IList`1"
            or "System.Collections.Generic.IReadOnlyCollection`1"
            or "System.Collections.Generic.IReadOnlyList`1"
            or "System.Collections.Generic.List`1"
            or "System.Collections.Generic.HashSet`1")
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }
        return false;
    }

    private static bool TryGetDictionaryValue(Type type, out Type valueType)
    {
        valueType = null!;
        if (!type.IsGenericType)
            return false;
        var definition = type.GetGenericTypeDefinition().FullName;
        if (definition is "System.Collections.Generic.IDictionary`2" or "System.Collections.Generic.Dictionary`2")
        {
            valueType = type.GetGenericArguments()[1];
            return true;
        }
        return false;
    }

    private static string ToCamel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private static bool IsJsonIgnored(PropertyInfo property)
    {
        foreach (var attribute in property.CustomAttributes)
        {
            if (!string.Equals(attribute.AttributeType.FullName, JsonIgnoreAttributeFullName, StringComparison.Ordinal))
                continue;

            var condition = attribute.NamedArguments
                .Where(argument => string.Equals(argument.MemberName, "Condition", StringComparison.Ordinal))
                .Select(argument => argument.TypedValue.Value?.ToString())
                .FirstOrDefault();
            return condition is null || string.Equals(condition, "Always", StringComparison.Ordinal);
        }

        return false;
    }

    private static string? ReadDescription(IEnumerable<CustomAttributeData> customAttributes) =>
        customAttributes
            .Where(attr => string.Equals(attr.AttributeType.FullName, DescriptionAttributeFullName, StringComparison.Ordinal))
            .Select(attr => attr.ConstructorArguments.Count == 1 ? attr.ConstructorArguments[0].Value as string : null)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
}

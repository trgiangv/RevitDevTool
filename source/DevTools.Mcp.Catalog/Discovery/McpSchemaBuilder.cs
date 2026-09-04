using DevTools.Mcp.Core.Protocol;
using System.Reflection;
using System.Text.Json.Nodes;

namespace DevTools.Mcp.Catalog.Discovery;

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

    /// <summary>Builds the useful JSON Schema subset without requiring the SDK runtime assembly.</summary>
    public static JsonObject BuildSchema(Type type, int depth = 0)
    {
        return depth > 4 ? [] : BuildSchemaNode(UnwrapReturnType(UnwrapNullable(type)), depth);
    }

    private static JsonObject BuildSchemaNode(Type type, int depth)
    {
        if (type.IsEnum)
            return BuildEnumSchema(type);

        if (TryGetCollectionElement(type, out var elementType))
            return BuildCollectionSchema(elementType, depth);

        if (TryGetDictionaryValue(type, out var valueType))
            return BuildDictionarySchema(valueType, depth);

        var primitive = FromClrType(type);
        if (primitive is JsonTypes.String or JsonTypes.Integer or JsonTypes.Number or JsonTypes.Boolean)
            return new JsonObject { [McpSpecKeys.JsonSchema.Type] = primitive };

        if (type == typeof(object))
            return [];

        return BuildObjectSchema(type, depth);
    }

    private static JsonObject BuildEnumSchema(Type type)
    {
        var values = new JsonArray();
        foreach (var value in Enum.GetNames(type))
            values.Add(value);
        return new JsonObject { [McpSpecKeys.JsonSchema.Type] = JsonTypes.String, ["enum"] = values };
    }

    private static JsonObject BuildCollectionSchema(Type elementType, int depth) =>
        new()
        {
            [McpSpecKeys.JsonSchema.Type] = JsonTypes.Array,
            [McpSpecKeys.JsonSchema.Items] = BuildSchema(elementType, depth + 1),
        };

    private static JsonObject BuildDictionarySchema(Type valueType, int depth) =>
        new()
        {
            [McpSpecKeys.JsonSchema.Type] = JsonTypes.Object,
            [McpSpecKeys.JsonSchema.AdditionalProperties] = BuildSchema(valueType, depth + 1),
        };

    private static JsonObject BuildObjectSchema(Type type, int depth)
    {
        var properties = new JsonObject();
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0 || IsJsonIgnored(property))
                continue;
            properties[ToCamel(property.Name)] = BuildSchema(property.PropertyType, depth + 1);
        }

        return properties.Count == 0
            ? []
            : new JsonObject
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
}

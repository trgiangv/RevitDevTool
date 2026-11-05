using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevTools.Mcp.Isolation;

/// <summary>
/// Writes MCP protocol objects that a toolset created with its own SDK copy, using that copy's serializer.
/// </summary>
public static class ToolsetProtocolJson
{
    private static readonly string ProtocolNamespace = typeof(CallToolResult).Namespace + ".";
    private static readonly string ContentBlockTypeName = typeof(ContentBlock).FullName!;
    private static readonly string JsonUtilitiesTypeName = typeof(McpJsonUtilities).FullName!;

    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    private static bool IsToolsetProtocolType(Type type) =>
        type.Assembly != typeof(CallToolResult).Assembly
        && type.FullName?.StartsWith(ProtocolNamespace, StringComparison.Ordinal) == true;

    public static bool ReturnsToolsetProtocolObject(MethodInfo method)
    {
        var type = method.ReturnType;
        if (type.IsGenericType && type.GetGenericTypeDefinition() is var definition
            && (definition == typeof(Task<>) || definition == typeof(ValueTask<>)))
        {
            type = type.GetGenericArguments()[0];
        }

        return IsToolsetProtocolType(type);
    }

    private static bool TrySerialize(object value, out string json)
    {
        json = string.Empty;
        var type = value.GetType();
        if (!IsToolsetProtocolType(type))
            return false;

        var options = type.Assembly
            .GetType(JsonUtilitiesTypeName)?
            .GetProperty(nameof(McpJsonUtilities.DefaultOptions), BindingFlags.Public | BindingFlags.Static)?
            .GetValue(null);
        if (options is null)
            return false;

        var serialize = options.GetType().Assembly
            .GetType(typeof(JsonSerializer).FullName!)?
            .GetMethod(nameof(JsonSerializer.Serialize), [typeof(object), typeof(Type), options.GetType()]);
        if (serialize?.Invoke(null, [value, ContractType(type), options]) is not string written)
            return false;

        json = written;
        return true;
    }

    private static Type ContractType(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.FullName, ContentBlockTypeName, StringComparison.Ordinal))
                return current;
        }

        return type;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
        options.Converters.Insert(0, new ToolsetProtocolConverterFactory());
        options.MakeReadOnly();
        return options;
    }

    private sealed class ToolsetProtocolConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => IsToolsetProtocolType(typeToConvert);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(ToolsetProtocolConverter<>).MakeGenericType(typeToConvert))!;
    }

    private sealed class ToolsetProtocolConverter<T> : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException($"Toolset protocol type '{typeToConvert.FullName}' is written only.");

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (value is null || !TrySerialize(value, out var json))
                throw new JsonException($"Could not write toolset protocol object '{typeof(T).FullName}'.");

            writer.WriteRawValue(json);
        }
    }
}

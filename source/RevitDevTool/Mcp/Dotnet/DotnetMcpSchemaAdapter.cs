using System.Reflection;
namespace RevitDevTool.Mcp.Dotnet;

internal static class DotnetMcpSchemaAdapter
{
    public static object BuildInputSchema(MethodInfo method)
    {
        var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var required = new List<string>();
        foreach (var parameter in method.GetParameters())
        {
            properties[parameter.Name ?? "arg"] = new
            {
                type = ToJsonType(parameter.ParameterType),
                description = $"Parameter {parameter.Name}"
            };

            if (!parameter.IsOptional)
                required.Add(parameter.Name ?? "arg");
        }

        return new
        {
            type = "object",
            properties,
            required
        };
    }

    private static string ToJsonType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
            return "integer";
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return "number";
        if (type == typeof(bool))
            return "boolean";
        if (type.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            return "array";
        if (type.IsClass && type != typeof(string))
            return "object";
        return "string";
    }
}
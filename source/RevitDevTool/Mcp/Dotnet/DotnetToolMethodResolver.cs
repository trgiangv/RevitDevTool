using System.IO;
using System.Reflection;
using ModelContextProtocol.Server;
using RevitDevTool.Mcp.Schemas;

namespace RevitDevTool.Mcp.Dotnet;

public sealed class DotnetToolMethodResolver
{
    public MethodInfo? Resolve(McpToolDefinition definition)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .OrderBy(GetAssemblyPath, StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assemblies)
        {
            if (!IsAssemblyMatch(assembly, definition.SourcePath))
                continue;

            foreach (var type in GetToolTypes(assembly))
            {
                if (!IsContainerMatch(type, definition.ContainerType))
                    continue;

                var method = FindToolMethod(type, definition);
                if (method is not null)
                    return method;
            }
        }

        return null;
    }

    private static bool IsAssemblyMatch(Assembly assembly, string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            return true;

        var assemblyPath = GetAssemblyPath(assembly);
        return string.IsNullOrWhiteSpace(assemblyPath) ||
               string.Equals(
                   Path.GetFullPath(assemblyPath),
                   Path.GetFullPath(sourcePath!),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsContainerMatch(Type type, string? containerType)
    {
        return string.IsNullOrWhiteSpace(containerType) ||
               string.Equals(type.FullName, containerType, StringComparison.Ordinal);
    }

    private static IEnumerable<Type> GetToolTypes(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
        }

        return types
            .OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase)
            .Where(type => type.IsDefined(typeof(McpServerToolTypeAttribute)));
    }

    private static MethodInfo? FindToolMethod(Type type, McpToolDefinition definition)
    {
        foreach (var method in type
                     .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                     .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
            if (toolAttr is null)
                continue;

            var toolName = !string.IsNullOrWhiteSpace(toolAttr.Name) ? toolAttr.Name : method.Name;
            if (!string.Equals(toolName, definition.Name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(definition.MethodName) &&
                !string.Equals(method.Name, definition.MethodName, StringComparison.Ordinal))
                continue;

            return method;
        }

        return null;
    }

    private static string? GetAssemblyPath(Assembly assembly)
    {
        try { return string.IsNullOrWhiteSpace(assembly.Location) ? null : assembly.Location; }
        catch { return null; }
    }
}

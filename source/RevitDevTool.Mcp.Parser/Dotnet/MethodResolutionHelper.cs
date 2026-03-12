using System.IO;
using System.Reflection;

namespace RevitDevTool.Mcp.Parser.Dotnet;

internal static class MethodResolutionHelper
{
    public static bool IsAssemblyMatch(Assembly assembly, string? sourcePath)
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

    public static bool IsContainerMatch(Type type, string? containerType)
    {
        return string.IsNullOrWhiteSpace(containerType) ||
               string.Equals(type.FullName, containerType, StringComparison.Ordinal);
    }

    public static IEnumerable<Type> GetTypesWithAttribute(Assembly assembly, Type attributeType)
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
            .Where(type => type.IsDefined(attributeType));
    }

    public static string? GetAssemblyPath(Assembly assembly)
    {
        try { return string.IsNullOrWhiteSpace(assembly.Location) ? null : assembly.Location; }
        catch { return null; }
    }
}

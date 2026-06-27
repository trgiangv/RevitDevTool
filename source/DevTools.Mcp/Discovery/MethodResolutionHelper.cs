using System.Reflection;

namespace DevTools.Mcp.Discovery;

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
                   // ReSharper disable once RedundantSuppressNullableWarningExpression
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
            // ReSharper disable once RedundantEnumerableCastCall
            types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
        }

        var attributeFullName = attributeType.FullName;
        return types
            .OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase)
            .Where(type => HasAttributeByName(type, attributeFullName));
    }

    private static bool HasAttributeByName(MemberInfo member, string? attributeFullName)
    {
        if (string.IsNullOrWhiteSpace(attributeFullName))
            return false;
        return member.CustomAttributes.Any(a =>
            string.Equals(a.AttributeType.FullName, attributeFullName, StringComparison.Ordinal));
    }

    public static string? GetAssemblyPath(Assembly assembly)
    {
        try { return string.IsNullOrWhiteSpace(assembly.Location) ? null : assembly.Location; }
        catch { return null; }
    }
}

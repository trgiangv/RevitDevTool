using System.Reflection;

namespace DevTool.McpParser.Dotnet;

internal static class MetadataAssemblyPathCollector
{
    public static IReadOnlyList<string> Collect(string assemblyPath)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddDllsFromDirectory(paths, Path.GetDirectoryName(assemblyPath));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(object).Assembly.Location));

        return paths.ToList();
    }

    private static void AddDllsFromDirectory(HashSet<string> paths, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return;

        try
        {
            foreach (var dll in Directory.GetFiles(directory, "*.dll"))
                paths.Add(dll);
        }
        catch
        {
            // Ignore unreadable directories during metadata-only resolution.
        }
    }

    public static IReadOnlyList<Type> GetMetadataTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // ReSharper disable once RedundantEnumerableCastCall
            return ex.Types.Where(type => type is not null).Cast<Type>().ToList();
        }
    }
}
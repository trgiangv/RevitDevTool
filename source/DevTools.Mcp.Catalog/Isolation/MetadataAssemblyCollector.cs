using System.Reflection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Isolation;

internal static class MetadataAssemblyCollector
{
    public static HashSet<string> Collect(string assemblyPath, IEnumerable<string>? dependencyPaths = null)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddDllsFromDirectory(paths, Path.GetDirectoryName(assemblyPath));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(object).Assembly.Location));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(McpAssemblyIsolationPlan).Assembly.Location));
        AddHostMcpAssemblyPaths(paths);
        AddLoadedAssemblyPaths(paths);
        AddExplicitPaths(paths, dependencyPaths);

        return paths;
    }

    private static void AddExplicitPaths(HashSet<string> paths, IEnumerable<string>? dependencyPaths)
    {
        if (dependencyPaths == null) return;

        foreach (var path in dependencyPaths)
            AddExplicitPath(paths, path);
    }

    private static void AddExplicitPath(HashSet<string> paths, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var fullPath = Path.GetFullPath(path);
        if (IsDllFile(fullPath))
        {
            paths.Add(fullPath);
            return;
        }

        if (!Directory.Exists(fullPath))
            throw new FileNotFoundException($"Metadata dependency path does not exist: '{path}'.", fullPath);

        AddDllsFromDirectory(paths, fullPath);
    }

    private static bool IsDllFile(string path) =>
        File.Exists(path) && string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// ILRepacked hosts embed MCP types; expose host-loaded MCP assembly paths for the metadata session.
    /// </summary>
    private static void AddHostMcpAssemblyPaths(HashSet<string> paths)
    {
        foreach (var assembly in new[] { typeof(CallToolResult).Assembly, typeof(McpServer).Assembly })
        {
            if (!string.IsNullOrEmpty(assembly.Location))
                paths.Add(assembly.Location);
        }
    }

    private static void AddLoadedAssemblyPaths(HashSet<string> paths)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                paths.Add(assembly.Location);
        }
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
            // ReSharper disable once UseCollectionExpression
            return ex.Types.Where(type => type is not null).Cast<Type>().ToArray();
        }
    }
}

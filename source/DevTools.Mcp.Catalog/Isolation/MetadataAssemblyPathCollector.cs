using System.Reflection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Catalog.Isolation;

internal static class MetadataAssemblyPathCollector
{
    public static IReadOnlyList<string> Collect(string assemblyPath, IEnumerable<string>? dependencyPaths = null)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddDllsFromDirectory(paths, Path.GetDirectoryName(assemblyPath));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(object).Assembly.Location));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(McpToolsetIsolationPlan).Assembly.Location));
        AddHostMcpAssemblyPaths(paths);
        AddLoadedAssemblyPaths(paths);
        AddExplicitPaths(paths, dependencyPaths);

        return paths.ToList();
    }

    private static void AddExplicitPaths(HashSet<string> paths, IEnumerable<string>? dependencyPaths)
    {
        if (dependencyPaths is null)
            return;

        foreach (var path in dependencyPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath) && string.Equals(Path.GetExtension(fullPath), ".dll", StringComparison.OrdinalIgnoreCase))
                paths.Add(fullPath);
            else if (Directory.Exists(fullPath))
                AddDllsFromDirectory(paths, fullPath);
            else
                throw new FileNotFoundException($"Metadata dependency path does not exist: '{path}'.", fullPath);
        }
    }

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
            return ex.Types.Where(type => type is not null).Cast<Type>().ToList();
        }
    }
}

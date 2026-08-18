using System.Reflection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Catalog.Discovery;

internal static class MetadataAssemblyPathCollector
{
    public static IReadOnlyList<string> Collect(string assemblyPath)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddDllsFromDirectory(paths, Path.GetDirectoryName(assemblyPath));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(object).Assembly.Location));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(McpAssemblyParser).Assembly.Location));
        AddHostMcpAssemblyPaths(paths);

        return paths.ToList();
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

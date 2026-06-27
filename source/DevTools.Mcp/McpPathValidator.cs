using System.Diagnostics;
using DevTools.Settings.Configs;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Mcp;

public static class McpPathValidator
{
    public const string PythonToolPattern = "*mcp.py";
    private const string DotnetToolPattern = ".dll";

    public static ExecutionMode ClassifyInputPath(string path)
    {
        if (IsValidDotnetAssemblyPath(path))
            return ExecutionMode.Dotnet;
        if (IsValidPythonToolsetPath(path))
            return ExecutionMode.Python;

        return ExecutionMode.Unsupported;
    }

    public static bool IsValidDotnetAssemblyPath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && File.Exists(path)
        && string.Equals(Path.GetExtension(path), DotnetToolPattern, StringComparison.OrdinalIgnoreCase);

    public static bool IsValidPythonToolsetPath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Directory.Exists(path)
        && Directory.EnumerateFiles(path!, PythonToolPattern, SearchOption.AllDirectories).Any();

    public static bool PathProducesCatalogItems(string path, ExecutionMode mode, McpRegistryCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = NormalizePath(path);
        return catalog.Tools.Any(t => t.Binding.SourceKind == mode && IsItemFromPath(normalized, t.Binding.SourcePath))
            || catalog.Prompts.Any(t => t.Binding.SourceKind == mode && IsItemFromPath(normalized, t.Binding.SourcePath))
            || catalog.Resources.Any(t => t.Binding.SourceKind == mode && IsItemFromPath(normalized, t.Binding.SourcePath));
    }

    private static bool IsItemFromPath(string configuredPath, string? itemSourcePath)
    {
        if (string.IsNullOrWhiteSpace(itemSourcePath))
            return false;

        var normalizedItem = NormalizePath(itemSourcePath!);
        if (string.Equals(configuredPath, normalizedItem, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!Directory.Exists(configuredPath))
            return false;

        var withSep = configuredPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedItem.StartsWith(withSep, StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveInvalidPaths(List<string> paths, ExecutionMode mode, McpRegistryCatalog catalog)
    {
        for (var i = paths.Count - 1; i >= 0; i--)
        {
            if (PathProducesCatalogItems(paths[i], mode, catalog))
                continue;

            Trace.TraceInformation($"[MCP] Remove saved {mode} path '{paths[i]}' because it loaded no primitives.");
            paths.RemoveAt(i);
        }
    }

    public static void PruneInvalidConfiguredPaths(
        McpRegistryConfig config,
        McpRegistryCatalog loadedCatalog)
    {
        RemoveInvalidPaths(config.DotnetPaths, ExecutionMode.Dotnet, loadedCatalog);
        RemoveInvalidPaths(config.PythonToolsetPaths, ExecutionMode.Python, loadedCatalog);
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static void AddDistinct(List<string> paths, string path)
    {
        if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase))
            paths.Add(path);
    }

    public static List<string> ResolvePaths(IEnumerable<string> paths, Func<string?, bool> validator) =>
        paths
            .Where(validator)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
}

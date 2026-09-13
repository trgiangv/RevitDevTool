using System.IO;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.Execution.PyRevit;

/// <summary>
/// Walks from a script directory up to <c>*.extension</c> and collects hierarchy <c>lib</c>/<c>bin</c> folders.
/// </summary>
internal static class PyRevitExtensionPaths
{
    private const string LibDir = "lib";
    private const string BinDir = "bin";
    private const string ExtensionSuffix = ".extension";
    private const int MaxExtensionAncestorDepth = 12;

    /// <summary>
    /// Yields component folders from extension root down to the script directory
    /// (libs first, then bins — matches pyRevit <c>CollectLibraryPaths</c> / <c>CollectBinaryPaths</c>).
    /// </summary>
    internal static IEnumerable<string> EnumerateHierarchyPaths(string? scriptDirectory)
    {
        var chain = BuildAncestorChain(scriptDirectory);
        foreach (var path in EnumerateComponentPaths(chain, LibDir))
            yield return path;

        foreach (var path in EnumerateComponentPaths(chain, BinDir))
            yield return path;
    }

    /// <summary>
    /// DLL files under hierarchy <c>lib</c> (any depth) and <c>bin</c> subfolders
    /// (not files sitting in the bin root).
    /// </summary>
    internal static IEnumerable<string> EnumerateDllCandidates(string? scriptDirectory)
    {
        var chain = BuildAncestorChain(scriptDirectory);

        foreach (var folderPath in EnumerateComponentPaths(chain, LibDir))
        {
            foreach (var dll in EnumerateAllDlls(folderPath))
                yield return dll;
        }

        foreach (var folderPath in EnumerateComponentPaths(chain, BinDir))
        {
            foreach (var dll in EnumerateSubfolderDlls(folderPath))
                yield return dll;
        }
    }

    internal static string? FindExtensionRoot(string? startDirectory)
    {
        foreach (var directory in BuildAncestorChain(startDirectory))
        {
            if (IsExtensionRoot(directory))
                return directory;
        }

        return null;
    }

    /// <summary>
    /// Directory whose modules should be dropped before the next Run:
    /// <c>*.extension</c> when present, otherwise the script directory.
    /// </summary>
    internal static string? ModuleRefreshRoot(string scriptPath)
    {
        var dir = Path.GetDirectoryName(scriptPath);
        return string.IsNullOrEmpty(dir) ? null : FindExtensionRoot(dir) ?? dir;
    }

    private static IEnumerable<string> EnumerateSubfolderDlls(string binRoot) =>
        Directory.EnumerateDirectories(binRoot)
            .SelectMany(subDir => Directory.EnumerateFiles(subDir, "*.dll", SearchOption.AllDirectories));

    /// <summary>Script directory first, then parents until <c>*.extension</c> (inclusive).</summary>
    private static List<string> BuildAncestorChain(string? startDirectory)
    {
        var chain = new List<string>();
        if (string.IsNullOrWhiteSpace(startDirectory))
            return chain;

        var dir = Path.GetFullPath(startDirectory);
        for (var depth = 0; depth < MaxExtensionAncestorDepth; depth++)
        {
            if (!TryAppendAncestor(chain, ref dir))
                break;
        }

        return chain;
    }

    private static bool TryAppendAncestor(List<string> chain, ref string? dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return false;

        chain.Add(dir!);

        if (IsExtensionRoot(dir!))
            return false;

        if (!TryGetParentDirectory(dir!, out var parent))
            return false;

        dir = parent;
        return true;
    }

    private static IEnumerable<string> EnumerateComponentPaths(List<string> chain, string componentDir)
    {
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var path = Path.Combine(chain[i], componentDir);
            if (Directory.Exists(path))
                yield return path;
        }
    }

    private static IEnumerable<string> EnumerateAllDlls(string directory) =>
        Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories);

    private static bool IsExtensionRoot(string dir) =>
        dir.EndsWith(ExtensionSuffix, StringComparison.OrdinalIgnoreCase);

    private static bool TryGetParentDirectory(string dir, out string? parent)
    {
        parent = Path.GetDirectoryName(dir);
        if (!string.IsNullOrEmpty(parent) && !string.Equals(parent, dir, StringComparison.OrdinalIgnoreCase)) 
            return true;
        
        parent = null;
        return false;
    }
}

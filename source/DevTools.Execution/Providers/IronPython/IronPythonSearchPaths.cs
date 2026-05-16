using System.IO;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Execution.Providers.IronPython;

/// <summary>
/// Builds <c>sys.path</c> entries for script execution.
/// </summary>
internal static class IronPythonSearchPaths
{
    private const string LibDir = "lib";
    
    internal static IReadOnlyList<string> ForNativeHost(string scriptPath, string? rootPath)
    {
        var paths = new List<string>();
        AppendScriptDirectory(paths, scriptPath);
        AppendToolsetRoot(paths, rootPath);
        return paths;
    }

    private static void AppendScriptDirectory(List<string> paths, string scriptPath)
    {
        var scriptDir = Path.GetDirectoryName(scriptPath);
        if (string.IsNullOrEmpty(scriptDir))
            return;

        AppendUnique(paths, scriptDir);
    }

    private static void AppendToolsetRoot(ICollection<string> paths, string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            return;

        var full = Path.GetFullPath(rootPath);
        if (IsLibDirectory(full))
        {
            AppendUnique(paths, full);
        }
        else
        {
            var libDir = FindLibDirectory(full);
            AppendUnique(paths, libDir ?? rootPath!);
        }
    }

    private static string? FindLibDirectory(string fromDirectory)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(fromDirectory));
        for (var dir = parent; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
        {
            if (IsLibDirectory(dir))
                return dir;
        }

        return null;
    }

    private static bool IsLibDirectory(string dir) =>
        Directory.Exists(dir)
        && string.Equals(Path.GetFileName(dir), LibDir, StringComparison.OrdinalIgnoreCase);

    private static void AppendUnique(ICollection<string> paths, string directory)
    {
        if (paths.Any(p => string.Equals(p, directory, StringComparison.OrdinalIgnoreCase)))
            return;

        paths.Add(directory);
    }
}

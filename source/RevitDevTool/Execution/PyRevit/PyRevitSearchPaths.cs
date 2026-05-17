using System.IO;
namespace RevitDevTool.Execution.PyRevit;

/// <summary>
/// Matches pyRevit <c>CommandTypeGenerator</c> search paths: script dir, hierarchy lib/bin,
/// pyrevitlib, site-packages. Does not add extension root, toolset paths, or engine folders from C#.
/// </summary>
internal static class PyRevitSearchPaths
{
    internal static IReadOnlyList<string> Build(string scriptPath, string? _)
    {
        var paths = new List<string>();

        var scriptDir = Path.GetDirectoryName(scriptPath);
        if (!string.IsNullOrEmpty(scriptDir))
            AppendUnique(paths, scriptDir);

        foreach (var directory in PyRevitExtensionPaths.EnumerateHierarchyPaths(scriptDir))
            AppendUnique(paths, directory);

        if (PyRevitLibraryPaths.TryResolve(out var install))
        {
            AppendUnique(paths, install.PyRevitLib);
            if (install.SitePackages is not null)
                AppendUnique(paths, install.SitePackages);
        }

        return paths;
    }

    private static void AppendUnique(ICollection<string> paths, string directory)
    {
        if (paths.Any(p => string.Equals(p, directory, StringComparison.OrdinalIgnoreCase)))
            return;

        paths.Add(directory);
    }
}

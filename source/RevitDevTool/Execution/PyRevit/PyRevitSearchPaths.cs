using System.IO;
using DevTools.Execution.Providers.IronPython;

namespace RevitDevTool.Execution.PyRevit;

/// <summary>
/// pyRevit command-generator paths (script dir, hierarchy lib/bin, pyrevitlib)
/// plus TFM-selected extension DLL directories, pydevd extract root first.
/// </summary>
internal static class PyRevitSearchPaths
{
    private const string PyRevitLibDir = "pyrevitlib";
    private const string SitePackagesDir = "site-packages";

    internal static List<string> Build(string scriptPath)
    {
        var paths = new List<string>();

        var scriptDir = Path.GetDirectoryName(scriptPath);
        if (!string.IsNullOrEmpty(scriptDir))
            AppendUnique(paths, scriptDir);

        foreach (var directory in PyRevitExtensionPaths.EnumerateHierarchyPaths(scriptDir))
            AppendUnique(paths, directory);

        foreach (var directory in PyRevitAssemblyLoader.SelectedAssemblyDirectories(scriptDir))
            AppendUnique(paths, directory);

        // IronPythonEngine.SetSearchPaths replaces sys.path; engine does not add core libs.
        var root = PyRevitLibraryPaths.InstallRoot;
        if (root is not null)
        {
            AppendIfExists(paths, Path.Combine(root, PyRevitLibDir));
            AppendIfExists(paths, Path.Combine(root, SitePackagesDir));
        }

        if (PydevdInstaller.IsInstalled())
        {
            var extract = PydevdInstaller.ExtractRoot;
            paths.RemoveAll(p => string.Equals(p, extract, StringComparison.OrdinalIgnoreCase));
            paths.Insert(0, extract);
        }

        return paths;
    }

    private static void AppendIfExists(ICollection<string> paths, string directory)
    {
        if (Directory.Exists(directory))
            AppendUnique(paths, directory);
    }

    private static void AppendUnique(ICollection<string> paths, string directory)
    {
        if (paths.Any(p => string.Equals(p, directory, StringComparison.OrdinalIgnoreCase)))
            return;

        paths.Add(directory);
    }
}

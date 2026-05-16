using System.IO;
using System.Reflection;

namespace RevitDevTool.HostAdapters.PyRevit;

/// <summary>
/// Resolves pyRevit install folders by walking up from <c>PyRevitLoader</c> (marker <c>pyRevitfile</c> or <c>pyrevitlib/</c>).
/// </summary>
internal static class PyRevitLibraryPaths
{
    private const string LoaderAssemblyName = "PyRevitLoader";
    private const string PyRevitLibDir = "pyrevitlib";
    private const string SitePackagesDir = "site-packages";
    private const string RootMarkerFile = "pyRevitfile";
    
    internal static bool IsLoaded => FindLoaderAssembly() is not null;

    internal readonly record struct Paths(string PyRevitLib, string? SitePackages);

    internal static bool TryResolve(out Paths paths)
    {
        paths = default;
        var home = FindPyRevitRootFromLoader();
        if (home is null)
            return false;

        var pyRevitLib = Path.Combine(home, PyRevitLibDir);
        if (!Directory.Exists(pyRevitLib))
            return false;

        var sitePackages = Path.Combine(home, SitePackagesDir);
        paths = new Paths(pyRevitLib, Directory.Exists(sitePackages) ? sitePackages : null);
        return true;
    }

    internal static Assembly? FindLoaderAssembly() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a =>
                string.Equals(a.GetName().Name, LoaderAssemblyName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Same idea as pyRevit <c>SessionManagerService.FindPyRevitRoot</c>: walk parents from the loader DLL directory.
    /// </summary>
    private static string? FindPyRevitRootFromLoader()
    {
        var loader = FindLoaderAssembly();
        if (loader is null)
            return null;

        var hint = Path.GetDirectoryName(loader.Location);
        if (string.IsNullOrEmpty(hint))
            return null;

        for (var dir = hint; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
        {
            if (File.Exists(Path.Combine(dir, RootMarkerFile))
                || Directory.Exists(Path.Combine(dir, PyRevitLibDir)))
                return dir;
        }

        return null;
    }
}

using System.IO;
using System.Reflection;
namespace RevitDevTool.Execution.PyRevit;

/// <summary>
/// Resolves pyRevit assemblies and install paths once per Revit session.
/// </summary>
internal static class PyRevitLibraryPaths
{
    private const string LoaderAssemblyName = "PyRevitLoader";
    private const string PyRevitLibDir = "pyrevitlib";
    private const string SitePackagesDir = "site-packages";
    private const string RootMarkerFile = "pyRevitfile";
    private const string RuntimeAssemblyPrefix = "PyRevitLabs.PyRevit.Runtime";

    private static readonly Lock ResolveLock = new();
    private static bool _resolved;
    private static Assembly? _loaderAssembly;
    private static Assembly? _runtimeAssembly;
    private static Paths? _installPaths;

    internal readonly record struct Paths(string PyRevitLib, string? SitePackages);

    internal static bool IsLoaded
    {
        get
        {
            EnsureResolved();
            return _loaderAssembly is not null;
        }
    }

    internal static bool IsRuntimeAvailable
    {
        get
        {
            EnsureResolved();
            return _runtimeAssembly is not null;
        }
    }

    internal static Assembly? LoaderAssembly
    {
        get
        {
            EnsureResolved();
            return _loaderAssembly;
        }
    }

    internal static Assembly? RuntimeAssembly
    {
        get
        {
            EnsureResolved();
            return _runtimeAssembly;
        }
    }

    internal static void EnsureResolved()
    {
        if (_resolved)
            return;

        lock (ResolveLock)
        {
            if (_resolved)
                return;

            _loaderAssembly = ScanAssemblies(static name =>
                string.Equals(name, LoaderAssemblyName, StringComparison.OrdinalIgnoreCase));

            _runtimeAssembly = ScanAssemblies(static name =>
                name.StartsWith(RuntimeAssemblyPrefix, StringComparison.OrdinalIgnoreCase));

            if (_loaderAssembly is not null)
                _installPaths = ResolveInstallPaths(_loaderAssembly);

            _resolved = true;
        }
    }

    internal static bool TryResolve(out Paths paths)
    {
        EnsureResolved();
        if (_installPaths is null)
        {
            paths = default;
            return false;
        }

        paths = _installPaths.Value;
        return true;
    }

    private static Assembly? ScanAssemblies(Func<string, bool> matches)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (name is not null && matches(name))
                return assembly;
        }

        return null;
    }

    private static Paths? ResolveInstallPaths(Assembly loader)
    {
        var hint = Path.GetDirectoryName(loader.Location);
        if (string.IsNullOrEmpty(hint))
            return null;

        for (var dir = hint; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
        {
            if (!File.Exists(Path.Combine(dir, RootMarkerFile))
                && !Directory.Exists(Path.Combine(dir, PyRevitLibDir)))
                continue;

            var pyRevitLib = Path.Combine(dir, PyRevitLibDir);
            if (!Directory.Exists(pyRevitLib))
                return null;

            var sitePackages = Path.Combine(dir, SitePackagesDir);
            return new Paths(pyRevitLib, Directory.Exists(sitePackages) ? sitePackages : null);
        }

        return null;
    }
}

using System.IO;
using System.Reflection;
namespace RevitDevTool.Execution.PyRevit;

/// <summary>
/// Resolves pyRevit assemblies and the install root (directory of <c>pyRevitfile</c>) once per Revit session.
/// </summary>
internal static class PyRevitLibraryPaths
{
    private const string PyRevitLibDir = "pyrevitlib";
    private const string RootMarkerFile = "pyRevitfile";

    private static readonly Lock ResolveLock = new();
    private static bool _resolved;
    private static Assembly? _loaderAssembly;
    private static Assembly? _runtimeAssembly;
    private static string? _installRoot;
    private static string _enginesDictKey = PyRevitNames.DefaultEnginesDictKey;

    internal static bool IsLoaded
    {
        get
        {
            EnsureResolved();
            return _loaderAssembly is not null;
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

    internal static string EnginesDictKey
    {
        get
        {
            EnsureResolved();
            return _enginesDictKey;
        }
    }

    internal static string? InstallRoot
    {
        get
        {
            EnsureResolved();
            return _installRoot;
        }
    }

    internal static IReadOnlyList<string> RefreshSkipRoots =>
        InstallRoot is { } root ? [root] : [];

    internal static void EnsureResolved()
    {
        if (_resolved)
            return;

        lock (ResolveLock)
        {
            if (_resolved)
                return;

            _loaderAssembly = ScanAssemblies(static name =>
                string.Equals(name, PyRevitNames.LoaderAssembly, StringComparison.OrdinalIgnoreCase));

            _runtimeAssembly = ScanAssemblies(static name =>
                name.StartsWith(PyRevitNames.RuntimePrefix, StringComparison.OrdinalIgnoreCase));

            _enginesDictKey = ReadEnginesDictKey(_runtimeAssembly) ?? PyRevitNames.DefaultEnginesDictKey;

            if (_loaderAssembly is not null)
                _installRoot = ResolveInstallRoot(_loaderAssembly);

            // pyRevit may load after this add-in; do not cache a miss.
            if (_loaderAssembly is not null || _runtimeAssembly is not null)
                _resolved = true;
        }
    }

    private static string? ReadEnginesDictKey(Assembly? runtime)
    {
        var keysType = runtime?.GetType(PyRevitNames.DomainStorageKeys);
        return keysType?.GetField(PyRevitNames.EnginesDictKey, BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null) as string;
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

    private static string? ResolveInstallRoot(Assembly loader)
    {
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

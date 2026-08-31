using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Python.Runtime;
using ZLogger;

namespace DevTools.Execution.Providers.Python;

/// <summary>
/// Host-process native search for the embedded CPython env.
/// Revit (and some other Autodesk hosts) ship empty <c>libssl-3-x64.dll</c> /
/// <c>libcrypto-3-x64.dll</c> stubs next to the exe (DLL-plant blockers).
/// Those win the default loader search over conda/pixi <c>Library\bin</c>, so
/// in-process <c>import ssl</c> fails with Win32 193. Preload the env copies
/// by absolute path before <see cref="PythonEngine.Initialize()"/>.
/// </summary>
public static class PythonNativeEnvironment
{
    private const uint LoadLibrarySearchDllLoadDir = 0x00000100;
    private const uint LoadLibrarySearchDefaultDirs = 0x00001000;

    /// <summary>
    /// PATH + <c>AddDllDirectory</c> + preload OpenSSL. Call before
    /// <see cref="PythonEngine.Initialize()"/>.
    /// </summary>
    public static void PrepareProcess(string pythonHome, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(pythonHome) || !Directory.Exists(pythonHome))
            return;

        var dirs = GetSearchDirectories(pythonHome);
        PrependToPath(dirs, logger);
        foreach (var dir in dirs)
            TryAddDllDirectory(dir, logger);

        foreach (var library in GetLibrariesToPreload(pythonHome))
            TryPreload(library, logger);
    }

    /// <summary>
    /// <c>os.add_dll_directory</c> for conda <c>Library\bin</c> (and siblings).
    /// Call with the GIL held, after <see cref="PythonEngine.Initialize()"/>.
    /// </summary>
    public static void AddPythonDllDirectories(string pythonHome)
    {
        if (string.IsNullOrWhiteSpace(pythonHome))
            return;

        using var os = Py.Import("os");
        if (!os.HasAttr("add_dll_directory"))
            return;

        using var add = os.GetAttr("add_dll_directory");
        foreach (var dir in GetSearchDirectories(pythonHome))
        {
            using var path = new PyString(dir);
            add.Invoke(path).Dispose();
        }
    }

    internal static IReadOnlyList<string> GetSearchDirectories(string pythonHome)
    {
        if (string.IsNullOrWhiteSpace(pythonHome))
            return [];

        return new[]
            {
                pythonHome,
                Path.Combine(pythonHome, "DLLs"),
                Path.Combine(pythonHome, "Library", "bin"),
            }
            .Where(Directory.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// OpenSSL copies to <c>LoadLibrary</c> before pythonnet starts. Crypto first
    /// (ssl depends on it). Same filename in later dirs is skipped.
    /// </summary>
    internal static IReadOnlyList<string> GetLibrariesToPreload(string pythonHome)
    {
        var dirs = GetSearchDirectories(pythonHome);
        var libraryBin = Path.GetFullPath(Path.Combine(pythonHome, "Library", "bin"));
        // Library\bin first so conda-forge OpenSSL wins over a copy next to python.exe.
        var preloadOrder = dirs
            .OrderByDescending(d => d.Equals(libraryBin, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var crypto = new List<string>();
        var ssl = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in preloadOrder)
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.dll"))
            {
                var name = Path.GetFileName(file);
                if (!seen.Add(name))
                    continue;

                if (name.StartsWith("libcrypto-", StringComparison.OrdinalIgnoreCase))
                    crypto.Add(file);
                else if (name.StartsWith("libssl-", StringComparison.OrdinalIgnoreCase))
                    ssl.Add(file);
            }
        }

        crypto.AddRange(ssl);
        return crypto;
    }

    private static void PrependToPath(IReadOnlyList<string> dirs, ILogger? logger)
    {
        var current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var toAdd = dirs
            .Where(d => current.IndexOf(d, StringComparison.OrdinalIgnoreCase) < 0)
            .ToList();

        if (toAdd.Count == 0)
            return;

        Environment.SetEnvironmentVariable("PATH", string.Join(";", toAdd) + ";" + current);
#if DEBUG
        logger?.ZLogInformation($"Prepended to PATH: {string.Join("; ", toAdd)}");
#endif
    }

    private static void TryAddDllDirectory(string directory, ILogger? logger)
    {
        var cookie = AddDllDirectory(directory);
        if (cookie != IntPtr.Zero)
            return;

        var error = Marshal.GetLastWin32Error();
        logger?.ZLogDebug($"AddDllDirectory('{directory}') failed (Win32 {error}).");
    }

    private static void TryPreload(string fullPath, ILogger? logger)
    {
        const uint flags = LoadLibrarySearchDllLoadDir | LoadLibrarySearchDefaultDirs;
        var handle = LoadLibraryEx(fullPath, IntPtr.Zero, flags);
        if (handle == IntPtr.Zero)
            handle = LoadLibrary(fullPath);

        if (handle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            logger?.ZLogWarning($"Failed to preload '{fullPath}' (Win32 {error}).");
            return;
        }

        var loadedFrom = GetModulePath(handle);
        if (string.IsNullOrEmpty(loadedFrom)
            || loadedFrom.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
            return;

        logger?.ZLogWarning(
            $"Native '{Path.GetFileName(fullPath)}' is already loaded from '{loadedFrom}' (wanted '{fullPath}'). In-process ssl may fail if the loaded copy is a host stub.");
    }

    private static string GetModulePath(IntPtr handle)
    {
        var buffer = new StringBuilder(32768);
        var length = GetModuleFileName(handle, buffer, buffer.Capacity);
        return length == 0 ? string.Empty : buffer.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr AddDllDirectory(string newDirectory);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, int nSize);
}

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Python.Runtime;
using ZLogger;
// ReSharper disable InconsistentNaming

namespace DevTools.Execution.Providers.Python;

/// <summary>Host CPython probe and Pixi native prep. See python-runtime.md.</summary>
public static class PythonNativeEnvironment
{
    private const string StableAbiForwarderFileName = "python3.dll";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int PyIsInitializedDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int PyGILStateEnsureDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void PyGILStateReleaseDelegate(int state);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr AddDllDirectory(string newDirectory);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, int nSize);

    /// <summary>PATH / DLL dir / OpenSSL preload for a Pixi-owned prefix. Call before <see cref="PythonEngine.Initialize()"/>.</summary>
    public static void PrepareProcess(string pythonHome, ILogger? logger = null)
    {
        if (TryGetLibraryBin(pythonHome) is not { } libraryBin)
            return;

        PrependToPath(libraryBin, logger);
        TryAddDllDirectory(libraryBin, logger);

        foreach (var library in GetLibrariesToPreload(pythonHome))
            Preload(library, logger);
    }

    /// <summary><c>os.add_dll_directory</c> for conda <c>Library\bin</c>. GIL held, after init.</summary>
    public static void AddPythonDllDirectories(string pythonHome)
    {
        if (TryGetLibraryBin(pythonHome) is not { } libraryBin)
            return;

        using var os = Py.Import("os");
        if (!os.HasAttr("add_dll_directory"))
            return;

        using var add = os.GetAttr("add_dll_directory");
        using var path = new PyString(libraryBin);
        add.Invoke(path).Dispose();
    }

    /// <summary>Loaded, initialized <c>python3xx.dll</c> (not the <c>python3.dll</c> forwarder).</summary>
    public static bool TryGetHostPythonDll(out string pythonDllPath)
    {
        var selected = SelectHostPythonDll(EnumerateInitializedPythonDlls());
        pythonDllPath = selected ?? string.Empty;
        return selected is not null;
    }

    public static string? TryGetHostPythonVersion()
        => SelectHostPythonVersion(EnumerateInitializedPythonDlls());

    internal static string? SelectHostPythonVersion(IEnumerable<string> initializedDlls)
        => SelectHostPythonDll(initializedDlls) is { } dll
           && TryGetCPythonVersion(dll, out var version)
            ? version
            : null;

    public static bool IsHostEmbedded() => TryGetHostPythonDll(out _);

    /// <summary><c>python313.dll</c> → <c>3.13</c>. Forwarder has no minor.</summary>
    internal static bool TryGetCPythonVersion(string pythonDllPath, out string version)
    {
        version = string.Empty;
        if (string.IsNullOrWhiteSpace(pythonDllPath))
            return false;

        var name = Path.GetFileName(pythonDllPath);
        var match = Regex.Match(
            name,
            @"^python3(?<minor>\d+)(?:_d|_t)?\.dll$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (match.Success)
        {
            version = "3." + match.Groups["minor"].Value;
            return true;
        }

        try
        {
            var info = FileVersionInfo.GetVersionInfo(pythonDllPath);
            if (info is { FileMajorPart: >= 3, FileMinorPart: >= 0 })
            {
                version = $"{info.FileMajorPart}.{info.FileMinorPart}";
                return true;
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or ArgumentException)
        {
        }

        return false;
    }

    /// <summary>Version of a loaded/prefix <c>python3xx.dll</c>. Forwarder: sibling versioned DLL.</summary>
    internal static string? ResolveHostVersion(string hostDll)
    {
        if (!IsStableAbiForwarder(hostDll)
            && TryGetCPythonVersion(hostDll, out var version))
            return version;

        var dir = Path.GetDirectoryName(hostDll);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return null;

        foreach (var dll in Directory.EnumerateFiles(dir, "python3*.dll"))
        {
            if (IsStableAbiForwarder(dll))
                continue;
            if (TryGetCPythonVersion(dll, out version))
                return version;
        }

        return null;
    }

    /// <summary>Host GIL + clear pythonnet <c>sys.clr_data</c> stash.</summary>
    public static void ClearPythonnetStash(string hostPythonDll)
    {
        WithHostGil(hostPythonDll, RuntimeData.ClearStash);
    }

    internal static string? SelectHostPythonDll(IEnumerable<string> initializedDlls)
    {
        string? forwarder = null;
        foreach (var path in initializedDlls)
        {
            if (!IsStableAbiForwarder(path)) return path;
            forwarder ??= path;
        }

        return forwarder;
    }

    internal static string? TryGetLibraryBin(string pythonHome)
    {
        if (string.IsNullOrWhiteSpace(pythonHome))
            return null;

        var libraryBin = Path.Combine(pythonHome, "Library", "bin");
        return Directory.Exists(libraryBin) ? Path.GetFullPath(libraryBin) : null;
    }

    internal static IReadOnlyList<string> GetLibrariesToPreload(string pythonHome)
    {
        if (TryGetLibraryBin(pythonHome) is not { } libraryBin)
            return [];

        var loaded = new List<string>(2);
        AddFirstMatch(loaded, libraryBin, "libcrypto-*.dll");
        AddFirstMatch(loaded, libraryBin, "libssl-*.dll");
        return loaded;
    }

    private static void AddFirstMatch(List<string> loaded, string directory, string pattern)
    {
        var match = Directory.EnumerateFiles(directory, pattern).FirstOrDefault();
        if (match is not null)
            loaded.Add(Path.GetFullPath(match));
    }

    private static IEnumerable<string> EnumerateInitializedPythonDlls()
    {
        foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
        {
            if (TryGetInitializedPythonPath(module, out var path))
                yield return path;
        }
    }

    private static bool TryGetInitializedPythonPath(ProcessModule module, out string path)
    {
        path = string.Empty;
        var handle = module.BaseAddress;
        if (handle == IntPtr.Zero)
            return false;

        if (!TryGetExport<PyIsInitializedDelegate>(handle, "Py_IsInitialized", out var pyIsInitialized))
            return false;

        if (pyIsInitialized() == 0)
            return false;

        var fileName = module.FileName;
        if (string.IsNullOrEmpty(fileName))
            return false;

        path = fileName;
        return true;
    }

    private static void WithHostGil(string hostPythonDll, Action action)
    {
        var handle = LoadLibrary(hostPythonDll);
        if (handle == IntPtr.Zero)
            return;

        if (!TryGetExport<PyGILStateEnsureDelegate>(handle, "PyGILState_Ensure", out var ensure)
            || !TryGetExport<PyGILStateReleaseDelegate>(handle, "PyGILState_Release", out var release))
            return;

        var gil = ensure();
        try
        {
            action();
        }
        finally
        {
            release(gil);
        }
    }

    private static bool TryGetExport<T>(IntPtr handle, string name, out T function)
        where T : Delegate
    {
        function = null!;
        var ptr = GetProcAddress(handle, name);
        if (ptr == IntPtr.Zero)
            return false;

        function = Marshal.GetDelegateForFunctionPointer<T>(ptr);
        return true;
    }

    internal static bool IsStableAbiForwarder(string path)
        => Path.GetFileName(path).Equals(StableAbiForwarderFileName, StringComparison.OrdinalIgnoreCase);

    private static void PrependToPath(string directory, ILogger? logger)
    {
        var current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (current.IndexOf(directory, StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        Environment.SetEnvironmentVariable("PATH", directory + ";" + current);
#if DEBUG
        logger?.ZLogInformation($"Prepended to PATH: {directory}");
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

    /// <summary>abi3 wheels import <c>python3.dll</c>; host often only mapped <c>python3xx.dll</c>.</summary>
    internal static void LoadStableAbiForwarder(string stdlibPrefix, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(stdlibPrefix))
            return;

        var forwarder = Path.Combine(stdlibPrefix, StableAbiForwarderFileName);
        if (!File.Exists(forwarder))
            return;

        Preload(Path.GetFullPath(forwarder), logger);
    }

    private static void Preload(string fullPath, ILogger? logger)
    {
        var handle = LoadLibrary(fullPath);
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
}

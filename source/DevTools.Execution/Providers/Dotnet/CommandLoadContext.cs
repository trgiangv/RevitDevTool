#if NET
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
namespace DevTools.Execution.Providers.Dotnet;

/// <summary>
/// Custom AssemblyLoadContext for loading add-in assemblies in isolation.
/// Host API assemblies are resolved from the default ALC by matching known names/prefixes.
/// </summary>
public class CommandLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "RevitAPI",
        "RevitAPIUI",
        "AdWindows",
        "acmgd",
        "acdbmgd",
        "accoremgd",
        "acdbmgdbrep"
    };

    private static readonly string[] SharedPrefixes =
    [
        "System.", "Microsoft.", "MahApps.", "ControlzEx.",
        "CommunityToolkit.", "Autodesk."
    ];

    private static readonly Lock InitLock = new();
    private static bool _initialized;

    /// <summary>
    /// Configures the shared assembly names from host API and host add-in directories.
    /// Must be called once during startup before any command loading.
    /// </summary>
    public static void Configure(string hostApiDirectory, string hostAddinDirectory)
    {
        lock (InitLock)
        {
            if (_initialized) return;
            PopulateSharedNames(hostApiDirectory);
            if (!string.Equals(hostApiDirectory, hostAddinDirectory, StringComparison.OrdinalIgnoreCase))
                PopulateSharedNames(hostAddinDirectory);
            _initialized = true;
        }
    }

    private static void PopulateSharedNames(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
        foreach (var dll in Directory.GetFiles(directory, "*.dll"))
            SharedAssemblyNames.Add(Path.GetFileNameWithoutExtension(dll));
    }

    private static bool IsSharedAssembly(string assemblyName)
    {
        if (SharedAssemblyNames.Contains(assemblyName))
            return true;

        foreach (var prefix in SharedPrefixes)
        {
            if (assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public CommandLoadContext(string pluginPath) : base(name: $"DevTools_{Guid.NewGuid():N}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        var pluginDirectory = Path.GetDirectoryName(pluginPath) ?? string.Empty;
        PreloadAssemblies(this, pluginDirectory);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name != null && IsSharedAssembly(assemblyName.Name))
            return null;

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPathStream(assemblyPath) : null;
    }

    private static void PreloadAssemblies(CommandLoadContext context, string pluginDirectory)
    {
        if (string.IsNullOrEmpty(pluginDirectory) || !Directory.Exists(pluginDirectory)) return;

        foreach (var dllPath in Directory.GetFiles(pluginDirectory, "*.dll"))
        {
            var simpleName = Path.GetFileNameWithoutExtension(dllPath);
            if (IsSharedAssembly(simpleName)) continue;
            context.LoadFromAssemblyPathStream(dllPath);
        }
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
    }

    private Assembly? LoadFromAssemblyPathStream(string assemblyPath)
    {
        try
        {
            var assemblyBytes = File.ReadAllBytes(assemblyPath);
            var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");

            if (File.Exists(pdbPath))
            {
                var pdbBytes = File.ReadAllBytes(pdbPath);
                using var asmStream = new MemoryStream(assemblyBytes);
                using var pdbStream = new MemoryStream(pdbBytes);
                return LoadFromStream(asmStream, pdbStream);
            }

            using var standaloneAsmStream = new MemoryStream(assemblyBytes);
            return LoadFromStream(standaloneAsmStream);
        }
        catch (Exception ex)
        {
            var fileName = Path.GetFileName(assemblyPath);
            Trace.TraceError($"{nameof(CommandLoadContext)} Failed to load '{fileName}': {ex.Message}");
            return null;
        }
    }
}
#endif

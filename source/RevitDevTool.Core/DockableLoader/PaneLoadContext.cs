#if NET
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
namespace RevitDevTool.Core.DockableLoader;

/// <summary>
///     Collectible <see cref="AssemblyLoadContext" /> dedicated to dockable / floating pane satellite UI.
/// </summary>
[PublicAPI]
public sealed class PaneLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] SharedPrefixes =
    [
        "System.", "Microsoft.", "MahApps.", "ControlzEx.",
        "CommunityToolkit.", "Autodesk."
    ];

    private static readonly Lock InitLock = new();
    private static bool _initialized;

    /// <summary>
    ///     Registers DLL simple names from host directories as shared with the default load context.
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

    public PaneLoadContext(string pluginPath) : base(name: $"RevitDevToolPane_{Guid.NewGuid():N}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        var pluginDirectory = Path.GetDirectoryName(pluginPath) ?? string.Empty;
        PreloadAssemblies(this, pluginDirectory, Path.GetFileName(pluginPath));
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name != null && IsSharedAssembly(assemblyName.Name))
            return null;

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPathStream(assemblyPath) : null;
    }

    private static void PreloadAssemblies(PaneLoadContext context, string pluginDirectory, string skipLoadedFileName)
    {
        if (string.IsNullOrEmpty(pluginDirectory) || !Directory.Exists(pluginDirectory)) return;

        foreach (var dllPath in Directory.GetFiles(pluginDirectory, "*.dll"))
        {
            if (string.Equals(Path.GetFileName(dllPath), skipLoadedFileName, StringComparison.OrdinalIgnoreCase))
                continue;

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
            Trace.TraceError($"{nameof(PaneLoadContext)} Failed to load '{fileName}': {ex.Message}");
            return null;
        }
    }
}
#endif

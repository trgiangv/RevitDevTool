#if NET
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
namespace RevitDevTool.Execution.Providers.Dotnet;

/// <summary>
/// Custom AssemblyLoadContext for loading add-in assemblies in isolation (Revit 2025 onward only).
/// </summary>
internal class CommandLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>
    /// Assemblies that must come from Default ALC — Revit folder + well-known prefixes.
    /// </summary>
    private static readonly HashSet<string> SharedAssemblyNames = BuildSharedAssemblyNames();

    /// <summary>
    /// Common 3rd party assembly prefixes that are expected to be shared and not loaded in isolation.
    /// </summary>
    private static readonly string[] SharedPrefixes =
    [
        "System.", "Microsoft.", "MahApps.", "ControlzEx.",
        "CommunityToolkit.", "Autodesk."
    ];

    private static HashSet<string> BuildSharedAssemblyNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var revitDir = Path.GetDirectoryName(typeof(IExternalCommand).Assembly.Location)!;
        var hostDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        
        foreach (var dll in Directory.GetFiles(revitDir, "*.dll"))
            names.Add(Path.GetFileNameWithoutExtension(dll));

        foreach (var dll in Directory.GetFiles(hostDir, "*.dll"))
            names.Add(Path.GetFileNameWithoutExtension(dll));
        
        return names;
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

    public CommandLoadContext(string pluginPath) : base(name: $"RevitDevTool_{Guid.NewGuid():N}", isCollectible: true)
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

    /// <summary>Loads assembly from bytes to avoid file locking.</summary>
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
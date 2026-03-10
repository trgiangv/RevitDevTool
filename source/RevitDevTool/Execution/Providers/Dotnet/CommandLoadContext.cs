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

    public CommandLoadContext(string pluginPath) : base(name: $"RevitDevTool_{Guid.NewGuid():N}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        var pluginDirectory = Path.GetDirectoryName(pluginPath) ?? string.Empty;
        PreloadAssemblies(this, pluginDirectory);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPathStream(assemblyPath) : null;
    }

    private static void PreloadAssemblies(CommandLoadContext context, string pluginDirectory)
    {
        if (string.IsNullOrEmpty(pluginDirectory) || !Directory.Exists(pluginDirectory)) return;

        var dllPaths = Directory.GetFiles(pluginDirectory, "*.dll");

        foreach (var dllPath in dllPaths)
        {
            context.LoadFromAssemblyPathStream(dllPath);
        }
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
    }

    /// <summary>
    /// Load a managed assembly from the specified path without keeping the file open.
    /// Reads the DLL (and optional PDB) into memory and calls LoadFromStream.
    /// </summary>
    /// <param name="assemblyPath">Full path to the managed assembly file.</param>
    /// <returns>The loaded Assembly instance.</returns>
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
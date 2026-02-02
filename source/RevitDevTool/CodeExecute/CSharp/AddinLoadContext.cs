#if NETCOREAPP
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace RevitDevTool.CodeExecute.CSharp;

/// <summary>
/// Custom AssemblyLoadContext for loading add-in assemblies in isolation (Revit 2025 onward only).
/// </summary>
internal class AddinLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly List<string> _nativeTempFiles = [];

    public AddinLoadContext(string pluginPath) : base(name: nameof(AddinLoadContext), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        var pluginDirectory = Path.GetDirectoryName(pluginPath) ?? string.Empty;
        PreloadAssemblies(this, pluginDirectory);

        // Try to clean up native temp files when the ALC starts unloading
        Unloading += _ =>
        {
            foreach (var temp in _nativeTempFiles)
            {
                try
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
                catch
                {
                    //ignore
                }
            }
            _nativeTempFiles.Clear();
        };
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name != null && (assemblyName.Name.StartsWith("RevitAPI")
                                          || assemblyName.Name.Contains("AdWindows")
                                          || assemblyName.Name.Contains("UIFramework")))
            return null;

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPathStream(assemblyPath) : null;
    }

    private static void PreloadAssemblies(AddinLoadContext context, string pluginDirectory)
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
        if (libraryPath == null) return IntPtr.Zero;

        try
        {
            var ext = Path.GetExtension(libraryPath);
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ext);
            File.Copy(libraryPath, tempFile, overwrite: true);
            _nativeTempFiles.Add(tempFile);
            return LoadUnmanagedDllFromPath(tempFile);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"{nameof(AddinLoadContext)} Failed to load unmanaged DLL '{unmanagedDllName}': {ex.Message}");
            return IntPtr.Zero;
        }
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
            Trace.TraceError($"{nameof(AddinLoadContext)} Failed to load '{fileName}': {ex.Message}");
            return null;
        }
    }
}
#endif

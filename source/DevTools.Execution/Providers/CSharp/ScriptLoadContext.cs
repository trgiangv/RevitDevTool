#if NET
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
namespace DevTools.Execution.Providers.CSharp;

/// <summary>
/// Collectible AssemblyLoadContext for C# script execution.
/// Preloads NuGet-resolved DLLs so script code can resolve them at runtime.
/// Host assemblies (Revit API, System.*, etc.) fall through to the default context.
/// </summary>
internal sealed class ScriptLoadContext : AssemblyLoadContext, IDisposable
{
    public ScriptLoadContext(IEnumerable<string> nugetDllPaths)
        : base($"CsxScript_{Guid.NewGuid():N}", isCollectible: true)
    {
        PreloadAssemblies(nugetDllPaths);
    }

    public Assembly LoadCompiledScript(byte[] peBytes)
    {
        using var stream = new MemoryStream(peBytes);
        return LoadFromStream(stream);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Returning null delegates resolution to the default ALC,
        // where host APIs (RevitAPI, System.*, etc.) are already loaded.
        return null;
    }

    private void PreloadAssemblies(IEnumerable<string> dllPaths)
    {
        foreach (var dllPath in dllPaths)
        {
            if (!File.Exists(dllPath)) continue;
            try
            {
                using var stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read);
                LoadFromStream(stream);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptLoadContext] Failed to preload '{Path.GetFileName(dllPath)}': {ex.Message}");
            }
        }
    }

    public void Dispose() => Unload();
}
#endif

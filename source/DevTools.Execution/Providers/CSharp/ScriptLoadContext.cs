#if NET
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Execution.Providers.CSharp;

/// <summary>
/// Collectible AssemblyLoadContext for C# script execution.
/// Lazily resolves NuGet DLLs selected during compilation.
/// Host assemblies (Revit API, System.*, etc.) fall through to the default context.
/// </summary>
internal sealed class ScriptLoadContext : AssemblyLoadContext, IDisposable
{
    private readonly Dictionary<string, string> _dependencyPaths;
    private readonly ILogger _logger;

    public ScriptLoadContext(IEnumerable<string> nugetDllPaths, ILogger logger)
        : base($"CsxScript_{Guid.NewGuid():N}", isCollectible: true)
    {
        _logger = logger;
        _dependencyPaths = BuildDependencyPathMap(nugetDllPaths);
    }

    public Assembly LoadCompiledScript(byte[] peBytes)
    {
        using var stream = new MemoryStream(peBytes);
        return LoadFromStream(stream);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is null)
            return null;

        if (!_dependencyPaths.TryGetValue(assemblyName.Name, out var dllPath))
            return null;

        try
        {
            return LoadFromAssemblyPath(dllPath);
        }
        catch (Exception ex)
        {
            _logger.ZLogDebug($"[ScriptLoadContext] Failed to load '{assemblyName.Name}' from '{dllPath}': {ex.Message}");
            return null;
        }
    }

    private Dictionary<string, string> BuildDependencyPathMap(IEnumerable<string> dllPaths)
    {
        var dependencyPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dllPath in dllPaths.Where(File.Exists))
        {
            var assemblyName = TryGetAssemblyName(dllPath);
            if (assemblyName is null)
                continue;

            dependencyPaths.TryAdd(assemblyName, Path.GetFullPath(dllPath));
        }

        return dependencyPaths;
    }

    private string? TryGetAssemblyName(string dllPath)
    {
        try
        {
            return AssemblyName.GetAssemblyName(dllPath).Name;
        }
        catch (Exception ex)
        {
            _logger.ZLogDebug($"[ScriptLoadContext] Failed to read assembly name from '{dllPath}': {ex.Message}");
            return null;
        }
    }

    public void Dispose() => Unload();
}
#endif

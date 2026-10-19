using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Mcp.Isolation;

/// <summary>
/// Manages per-toolset load contexts. One context per unique DLL path.
/// Thread-safe: GetOrCreate and Clear can be called concurrently.
/// </summary>
[UsedImplicitly]
public sealed class McpAssemblyContextManager(ILogger<McpAssemblyContextManager> logger) : IDisposable
{
    private volatile ConcurrentDictionary<string, Lazy<McpAssemblyContext>> _contexts = new(StringComparer.OrdinalIgnoreCase);

    public McpAssemblyContext GetOrCreate(string toolsetDllPath)
    {
        var normalizedPath = Path.GetFullPath(toolsetDllPath);
        var lazy = _contexts.GetOrAdd(normalizedPath,
            path => new Lazy<McpAssemblyContext>(() => new McpAssemblyContext(path, logger)));
        return lazy.Value;
    }

    /// <summary>
    /// Disposes all toolset contexts and prepares for fresh lazy creation.
    /// Caller should clear dispatcher caches BEFORE calling this method,
    /// and invoke PurgeReleasedAPIObjects + GC afterward if running inside Revit.
    /// </summary>
    public void Clear()
    {
        var snapshot = Interlocked.Exchange(
            ref _contexts,
            new ConcurrentDictionary<string, Lazy<McpAssemblyContext>>(StringComparer.OrdinalIgnoreCase));

        foreach (var entry in snapshot.Values)
        {
            if (!entry.IsValueCreated) continue;
            try
            {
                entry.Value.Dispose();
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"[McpContextManager] Failed to dispose context: {ex.Message}");
            }
        }
    }

    public void Dispose() => Clear();
}

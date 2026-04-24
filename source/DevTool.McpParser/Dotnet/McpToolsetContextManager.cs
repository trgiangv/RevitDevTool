using System.Collections.Concurrent;
using System.Diagnostics;
using JetBrains.Annotations;

namespace DevTool.McpParser.Dotnet;

/// <summary>
/// Manages per-toolset load contexts. One context per unique DLL path.
/// Thread-safe: GetOrCreate and Clear can be called concurrently.
/// </summary>
[UsedImplicitly]
public sealed class McpToolsetContextManager : IDisposable
{
    private volatile ConcurrentDictionary<string, Lazy<McpToolsetContext>> _contexts = new(StringComparer.OrdinalIgnoreCase);

    public McpToolsetContext GetOrCreate(string toolsetDllPath)
    {
        var normalizedPath = Path.GetFullPath(toolsetDllPath);
        var lazy = _contexts.GetOrAdd(normalizedPath,
            static path => new Lazy<McpToolsetContext>(() => new McpToolsetContext(path)));
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
            new ConcurrentDictionary<string, Lazy<McpToolsetContext>>(StringComparer.OrdinalIgnoreCase));

        foreach (var entry in snapshot.Values)
        {
            if (!entry.IsValueCreated) continue;
            try
            {
                entry.Value.Dispose();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[McpToolsetContextManager] Failed to dispose context: {ex.Message}");
            }
        }
    }

    public void Dispose() => Clear();
}

using System.Diagnostics;

namespace DevTools.Execution.Models;

/// <summary>
/// Unified cache entry for compiled scripts (C# and F#).
/// Holds a compiled command factory and optional cleanup disposables
/// (e.g., collectible AssemblyLoadContext for C#, FsiEvaluationSession + temp files for F#).
/// </summary>
internal sealed class CachedScript(string contentHash, Func<object> createCommand, params IDisposable?[] cleanups) : IDisposable
{
    public string ContentHash { get; } = contentHash;

    public object CreateCommand() => createCommand();

    public void Dispose()
    {
        foreach (var item in cleanups)
        {
            try
            {
                item?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CachedScript] Cleanup failed: {ex.Message}");
            }
        }
    }
}

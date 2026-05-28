namespace DevTools.Execution.Models;

/// <summary>
/// Unified cache entry for compiled scripts (C# and F#).
/// Holds the compiled command and optional cleanup disposables
/// (e.g., collectible AssemblyLoadContext for C#, FsiEvaluationSession + temp files for F#).
/// </summary>
internal sealed class CachedScript(string contentHash, object command, params IDisposable?[] cleanups) : IDisposable
{
    public string ContentHash { get; } = contentHash;
    public object Command { get; } = command;

    public void Dispose()
    {
        foreach (var item in cleanups)
            item?.Dispose();
    }
}

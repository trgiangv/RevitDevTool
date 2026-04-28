using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using DevTools.Execution.Interfaces;
using FSharp.Compiler.Interactive;
namespace DevTools.Execution.Providers.FSharp;

/// <summary>
/// Caches compiled F# scripts keyed by entry script path.
/// Reuses the compiled command when no file in the script graph has changed.
/// On invalidation, disposes the FsiEvaluationSession (releasing collectible assemblies on .NET Core)
/// </summary>
public static class FSharpCompilationCache
{
    private static readonly ConcurrentDictionary<string, CachedScript> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CompileLocks = new(StringComparer.OrdinalIgnoreCase);
    private static IFSharpHostSupport? _hostSupport;

    public static void Configure(IFSharpHostSupport hostSupport) => _hostSupport = hostSupport;

    public static async Task<object?> GetOrCompileAsync(
        string scriptPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var canonicalPath = Path.GetFullPath(scriptPath);
        var scriptName = Path.GetFileName(canonicalPath);

        var graph = await FSharpScriptGraph.BuildLoadGraphAsync(canonicalPath, ct).ConfigureAwait(false);
        var currentHash = FSharpScriptGraph.ComputeGraphHash(graph);

        if (Cache.TryGetValue(canonicalPath, out var cached) && cached.GraphHash == currentHash)
        {
            progress?.Report($"Using cached {scriptName}.");
            Debug.WriteLine($"[FSharpCache] Hit for '{Path.GetFileName(canonicalPath)}' (hash: {currentHash[..16]})");
            return cached.Command;
        }

        var gate = CompileLocks.GetOrAdd(canonicalPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Cache.TryGetValue(canonicalPath, out cached) && cached.GraphHash == currentHash)
            {
                progress?.Report($"Using cached {scriptName}.");
                Debug.WriteLine($"[FSharpCache] Hit (after lock) for '{Path.GetFileName(canonicalPath)}'");
                return cached.Command;
            }

            if (cached != null)
            {
                Debug.WriteLine($"[FSharpCache] Miss (hash changed) for '{Path.GetFileName(canonicalPath)}'");
                Cache.TryRemove(canonicalPath, out _);
                InvalidateEntry(cached);
            }
            else
            {
                Debug.WriteLine($"[FSharpCache] Miss (first compile) for '{Path.GetFileName(canonicalPath)}'");
            }

            var hostSupport = _hostSupport
                ?? throw new InvalidOperationException("FSharpCompilationCache has not been configured with host support.");

            var resolution = await FSharpDependencyResolver.ResolveAsync(canonicalPath, graph, hostSupport, progress, ct).ConfigureAwait(false);
            progress?.Report($"Compiling {scriptName}...");

            var result = FSharpExecutor.CreateSessionAndEvaluate(resolution.ScriptPath, resolution.References, hostSupport);
            if (result.Command == null)
            {
                (result.Session as IDisposable)?.Dispose();
                resolution.Cleanup?.Dispose();
                return null;
            }

            var entry = new CachedScript(currentHash, result.Command, result.Session, resolution.Cleanup);
            Cache[canonicalPath] = entry;

            Debug.WriteLine($"[FSharpCache] Cached '{Path.GetFileName(canonicalPath)}' (hash: {currentHash[..16]})");
            return result.Command;
        }
        finally
        {
            gate.Release();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InvalidateEntry(CachedScript entry)
    {
        entry.Dispose();

#if NET
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
#endif
    }
}

internal sealed class CachedScript(string graphHash, object command, Shell.FsiEvaluationSession? session, IDisposable? tempCleanup) : IDisposable
{
    public string GraphHash { get; } = graphHash;
    public object Command { get; } = command;
    private Shell.FsiEvaluationSession? Session { get; } = session;
    private IDisposable? TempCleanup { get; } = tempCleanup;

    public void Dispose()
    {
        (Session as IDisposable)?.Dispose();
        TempCleanup?.Dispose();
    }
}

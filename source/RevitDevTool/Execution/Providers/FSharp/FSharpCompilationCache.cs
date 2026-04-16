using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using FSharp.Compiler.Interactive;
namespace RevitDevTool.Execution.Providers.FSharp;

/// <summary>
/// Caches compiled F# scripts keyed by entry script path.
/// Reuses the compiled IExternalCommand when no file in the script graph has changed.
/// On invalidation, disposes the FsiEvaluationSession (releasing collectible assemblies on .NET Core)
/// </summary>
internal static class FSharpCompilationCache
{
    private static readonly ConcurrentDictionary<string, CachedScript> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CompileLocks = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<IExternalCommand?> GetOrCompileAsync(
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

            var resolution = await FSharpDependencyResolver.ResolveAsync(canonicalPath, graph, progress, ct).ConfigureAwait(false);
            progress?.Report($"Compiling {scriptName}...");

            var result = FSharpExecutor.CreateSessionAndEvaluate(resolution.ScriptPath, resolution.References);
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

    /// <summary>
    /// Disposes the FSI session (releases collectible ALC on .NET Core, frees dynamic assembly on .NET Framework)
    /// and forces GC to reclaim unreachable assemblies.
    /// NoInlining prevents the JIT from keeping local references that would root the collectible assemblies.
    /// </summary>
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

internal sealed class CachedScript(string graphHash, IExternalCommand command, Shell.FsiEvaluationSession? session, IDisposable? tempCleanup) : IDisposable
{
    public string GraphHash { get; } = graphHash;
    public IExternalCommand Command { get; } = command;
    private Shell.FsiEvaluationSession? Session { get; } = session;
    private IDisposable? TempCleanup { get; } = tempCleanup;

    public void Dispose()
    {
        (Session as IDisposable)?.Dispose();
        TempCleanup?.Dispose();
    }
}

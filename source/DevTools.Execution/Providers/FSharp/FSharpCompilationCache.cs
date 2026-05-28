using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
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

    public static async Task<ScriptCompilationResult> GetOrCompileAsync(
        string scriptPath,
        ICompiledScriptBridge bridgeSupport,
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
            return ScriptCompilationResult.Succeeded(cached.Command);
        }

        var gate = CompileLocks.GetOrAdd(canonicalPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Cache.TryGetValue(canonicalPath, out cached) && cached.GraphHash == currentHash)
            {
                progress?.Report($"Using cached {scriptName}.");
                Debug.WriteLine($"[FSharpCache] Hit (after lock) for '{Path.GetFileName(canonicalPath)}'");
                return ScriptCompilationResult.Succeeded(cached.Command);
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

            var resolution = await FSharpDependencyResolver.ResolveAsync(canonicalPath, graph, bridgeSupport, progress, ct).ConfigureAwait(false);
            progress?.Report($"Compiling {scriptName}...");

            var output = FSharpExecutor.CreateSessionAndEvaluate(resolution.ScriptPath, resolution.References, bridgeSupport);
            if (output.Command == null)
            {
                (output.Session as IDisposable)?.Dispose();
                resolution.Cleanup?.Dispose();
                return ScriptCompilationResult.Failed("No executable command type found in F# script.");
            }

            var entry = new CachedScript(currentHash, output.Command, output.Session, resolution.Cleanup);
            Cache[canonicalPath] = entry;

            Debug.WriteLine($"[FSharpCache] Cached '{Path.GetFileName(canonicalPath)}' (hash: {currentHash[..16]})");
            return ScriptCompilationResult.Succeeded(output.Command);
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

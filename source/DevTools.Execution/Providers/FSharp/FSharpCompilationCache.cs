using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using DevTools.Execution.Models;
using Microsoft.Extensions.Logging;
using ZLogger;
namespace DevTools.Execution.Providers.FSharp;

/// <summary>
/// Caches compiled F# scripts keyed by entry script path.
/// Reuses the compiled command when no file in the script graph has changed.
/// On invalidation, disposes the FsiEvaluationSession (releasing collectible assemblies on .NET Core)
/// and any temp files from NuGet resolution.
/// </summary>
public sealed class FSharpCompilationCache(
    ICompiledScriptBridge bridge,
    FSharpDependencyResolver dependencyResolver,
    FSharpExecutor executor,
    ILogger<FSharpCompilationCache> logger)
{
    private readonly ConcurrentDictionary<string, CachedScript> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _compileLocks = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ScriptCompilationResult> GetOrCompileAsync(
        string scriptPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var canonicalPath = Path.GetFullPath(scriptPath);
        var scriptName = Path.GetFileName(canonicalPath);

        var graph = await FSharpScriptGraph.BuildLoadGraphAsync(canonicalPath, ct).ConfigureAwait(false);
        var currentHash = FSharpScriptGraph.ComputeGraphHash(graph);

        if (_cache.TryGetValue(canonicalPath, out var cached) && cached.ContentHash == currentHash)
        {
            progress?.Report($"Using cached {scriptName}.");
            logger.ZLogDebug($"[FSharpCache] Hit for '{scriptName}' (hash: {currentHash[..16]})");
            return ScriptCompilationResult.Succeeded(cached.CreateCommand());
        }

        var gate = _compileLocks.GetOrAdd(canonicalPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(canonicalPath, out cached) && cached.ContentHash == currentHash)
            {
                progress?.Report($"Using cached {scriptName}.");
                logger.ZLogDebug($"[FSharpCache] Hit (after lock) for '{scriptName}'");
                return ScriptCompilationResult.Succeeded(cached.CreateCommand());
            }

            if (cached != null)
            {
                logger.ZLogDebug($"[FSharpCache] Miss (hash changed) for '{scriptName}'");
                _cache.TryRemove(canonicalPath, out _);
                InvalidateEntry(cached);
            }
            else
            {
                logger.ZLogDebug($"[FSharpCache] Miss (first compile) for '{scriptName}'");
            }

            var resolution = await dependencyResolver.ResolveAsync(canonicalPath, graph, bridge, progress, ct).ConfigureAwait(false);
            progress?.Report($"Compiling {scriptName}...");

            var output = executor.CreateSessionAndEvaluate(resolution.ScriptPath, resolution.References, bridge);
            if (output.Command == null)
            {
                (output.Session as IDisposable)?.Dispose();
                resolution.Cleanup?.Dispose();
                return ScriptCompilationResult.Failed("No executable command type found in F# script.");
            }

            var commandType = output.Command.GetType();
            var entry = new CachedScript(
                currentHash,
                () => Activator.CreateInstance(commandType)
                      ?? throw new InvalidOperationException($"Failed to create instance of {commandType.FullName}."),
                output.Session,
                resolution.Cleanup)
            {
                Logger = logger
            };
            _cache[canonicalPath] = entry;

            logger.ZLogDebug($"[FSharpCache] Cached '{scriptName}' (hash: {currentHash[..16]})");
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

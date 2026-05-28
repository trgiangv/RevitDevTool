using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;

namespace DevTools.Execution.Providers.CSharp;

/// <summary>
/// Caches compiled C# scripts keyed by combined hash of entry file + all #load dependencies.
/// Recompiles when any file in the graph changes.
/// </summary>
internal static class CSharpCompilationCache
{
    private static readonly ConcurrentDictionary<string, CachedCSharpScript> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CompileLocks = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<ScriptCompilationResult> GetOrCompileAsync(
        string scriptPath,
        ICompiledScriptBridge bridgeSupport,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var canonicalPath = Path.GetFullPath(scriptPath);
        var scriptName = Path.GetFileName(canonicalPath);

        var currentHash = await ComputeGraphHashAsync(canonicalPath, bridgeSupport, ct).ConfigureAwait(false);

        if (Cache.TryGetValue(canonicalPath, out var cached) && cached.ContentHash == currentHash)
        {
            progress?.Report($"Using cached {scriptName}.");
            Debug.WriteLine($"[CSharpCache] Hit for '{scriptName}' (hash: {currentHash[..16]})");
            return ScriptCompilationResult.Succeeded(cached.Command);
        }

        var gate = CompileLocks.GetOrAdd(canonicalPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Cache.TryGetValue(canonicalPath, out cached) && cached.ContentHash == currentHash)
            {
                progress?.Report($"Using cached {scriptName}.");
                Debug.WriteLine($"[CSharpCache] Hit (after lock) for '{scriptName}'");
                return ScriptCompilationResult.Succeeded(cached.Command);
            }

            if (cached != null)
            {
                Debug.WriteLine($"[CSharpCache] Miss (hash changed) for '{scriptName}'");
                Cache.TryRemove(canonicalPath, out _);
                InvalidateEntry();
            }
            else
            {
                Debug.WriteLine($"[CSharpCache] Miss (first compile) for '{scriptName}'");
            }

            var result = await CSharpCompiler.CompileAsync(canonicalPath, bridgeSupport, progress, ct).ConfigureAwait(false);

            if (result is { Success: true, Command: not null })
            {
                Cache[canonicalPath] = new CachedCSharpScript(currentHash, result.Command);
                Debug.WriteLine($"[CSharpCache] Cached '{scriptName}' (hash: {currentHash[..16]})");
            }

            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InvalidateEntry()
    {
#if NET
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
#endif
    }

    private static Task<string> ComputeGraphHashAsync(string entryPath, ICompiledScriptBridge bridgeSupport, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var graph = CSharpDirectiveParser.ResolveGraph(
            entryPath,
            bridgeSupport.GetHostReferencePattern(),
            bridgeSupport.GetHostReferenceReplacement());

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in graph.SourceFiles)
        {
            var bytes = File.ReadAllBytes(file.Path);
            hasher.AppendData(bytes);
        }

        var hash = hasher.GetHashAndReset();
        return Task.FromResult(Convert.ToHexString(hash));
    }
}

internal sealed class CachedCSharpScript(string contentHash, object command)
{
    public string ContentHash { get; } = contentHash;
    public object Command { get; } = command;
}

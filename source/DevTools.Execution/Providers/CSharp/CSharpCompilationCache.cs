using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using DevTools.Execution.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Execution.Providers.CSharp;

/// <summary>
/// Caches compiled C# scripts keyed by combined hash of entry file + all #load dependencies.
/// Recompiles when any file in the graph changes. On .NET Core+, cache eviction
/// disposes the collectible AssemblyLoadContext to release compiled assemblies.
/// </summary>
public sealed class CSharpCompilationCache(
    ICompiledScriptBridge bridge,
    CSharpCompiler compiler,
    ILogger<CSharpCompilationCache> logger)
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

        var currentHash = ComputeGraphHash(canonicalPath, ct);

        if (_cache.TryGetValue(canonicalPath, out var cached) && cached.ContentHash == currentHash)
        {
            progress?.Report($"Using cached {scriptName}.");
            LogDebug($"Hit for '{scriptName}' (hash: {currentHash[..16]})");
            return ScriptCompilationResult.Succeeded(cached.CreateCommand());
        }

        var gate = _compileLocks.GetOrAdd(canonicalPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(canonicalPath, out cached) && cached.ContentHash == currentHash)
            {
                progress?.Report($"Using cached {scriptName}.");
                LogDebug($"Hit (after lock) for '{scriptName}'");
                return ScriptCompilationResult.Succeeded(cached.CreateCommand());
            }

            if (cached != null)
            {
                LogDebug($" Miss (hash changed) for '{scriptName}'");
                _cache.TryRemove(canonicalPath, out _);
                InvalidateEntry(cached);
            }
            else
            {
                LogDebug($"Miss (first compile) for '{scriptName}'");
            }

            var result = await compiler.CompileAsync(canonicalPath, bridge, progress, ct).ConfigureAwait(false);

            if (result is not { Success: true, Command: not null })
                return result;

            var commandType = result.Command.GetType();
            _cache[canonicalPath] = new CachedScript(
                currentHash,
                () => Activator.CreateInstance(commandType)
                      ?? throw new InvalidOperationException($"Failed to create instance of {commandType.FullName}."),
                result.Cleanup)
            {
                Logger = logger
            };
            LogDebug($"Cached '{scriptName}' (hash: {currentHash[..16]})");

            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    [Conditional("DEBUG")]
    private void LogDebug(string message) => logger.ZLogDebug($"{message}");

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

    private static string ComputeGraphHash(string entryPath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var graph = CSharpDirectiveParser.ResolveGraph(entryPath);

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in graph.SourceFiles.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
        {
            var bytes = File.ReadAllBytes(file.Path);
            hasher.AppendData(bytes);
            hasher.AppendData([0]);
        }

        var hash = hasher.GetHashAndReset();
        return Convert.ToHexString(hash);
    }
}

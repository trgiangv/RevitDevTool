using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using DevTools.Execution.Interfaces;

namespace DevTools.Execution.Providers.CSharp;

/// <summary>
/// Caches compiled C# scripts keyed by file content hash.
/// Recompiles only when the source file changes.
/// </summary>
internal static class CSharpCompilationCache
{
    private static readonly ConcurrentDictionary<string, CachedCSharpScript> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CompileLocks = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<CSharpCompilationResult> GetOrCompileAsync(
        string scriptPath,
        IFSharpHostSupport hostSupport,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var canonicalPath = Path.GetFullPath(scriptPath);
        var scriptName = Path.GetFileName(canonicalPath);

        var currentHash = await ComputeFileHashAsync(canonicalPath, ct).ConfigureAwait(false);

        if (Cache.TryGetValue(canonicalPath, out var cached) && cached.ContentHash == currentHash)
        {
            progress?.Report($"Using cached {scriptName}.");
            Debug.WriteLine($"[CSharpCache] Hit for '{scriptName}' (hash: {currentHash[..16]})");
            return CSharpCompilationResult.Succeeded(cached.Command);
        }

        var gate = CompileLocks.GetOrAdd(canonicalPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Cache.TryGetValue(canonicalPath, out cached) && cached.ContentHash == currentHash)
            {
                progress?.Report($"Using cached {scriptName}.");
                Debug.WriteLine($"[CSharpCache] Hit (after lock) for '{scriptName}'");
                return CSharpCompilationResult.Succeeded(cached.Command);
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

            var result = await CSharpCompiler.CompileAsync(canonicalPath, hostSupport, progress, ct).ConfigureAwait(false);

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

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct)
    {
        using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hashBytes);
    }
}

internal sealed class CachedCSharpScript(string contentHash, object command)
{
    public string ContentHash { get; } = contentHash;
    public object Command { get; } = command;
}

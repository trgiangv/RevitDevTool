using System.Diagnostics;
using System.IO;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using Microsoft.Extensions.Logging;
using ZLogger;
namespace DevTools.Execution.Providers.FSharp;

/// <summary>
/// Execution strategy for F# scripts (.fsx).
/// Compiles via FSharpCompilationCache (with graph-level caching), executes via host context.
/// </summary>
public sealed class FSharpExecutionStrategy(
    string scriptPath,
    IHostContextExecutor hostContext,
    ICommandRunner commandRunner,
    FSharpCompilationCache cache,
    ILogger<FSharpExecutionStrategy> logger) : IExecutionStrategy
{
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(30);

    public async Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var scriptName = Path.GetFileName(scriptPath);
        try
        {
            progress?.Report($"Preparing {scriptName}...");
            var compilationResult = await CompileAsync(scriptPath, progress, cancellationToken).ConfigureAwait(false);

            if (compilationResult == null)
                return ExecutionResult.Failed($"F# compilation timeout after {CompileTimeout.TotalSeconds:0}s for '{scriptName}'.", durationMs: stopwatch.ElapsedMilliseconds);

            if (!compilationResult.Success || compilationResult.Command == null)
                return ExecutionResult.Failed(compilationResult.FormatDiagnostics($"F# compilation failed for '{scriptName}'."), durationMs: stopwatch.ElapsedMilliseconds);

            progress?.Report($"Running {scriptName}...");
            var result = await hostContext
                .ExecuteAsync(() =>
                {
                    var execResult = commandRunner.RunCompiledCommand(compilationResult.Command);
                    stopwatch.Stop();
                    return execResult;
                }, cancellationToken)
                .ConfigureAwait(false);

            progress?.Report(result.Success
                ? $"Completed {scriptName}."
                : result.Message);

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return ExecutionResult.Cancelled("F# execution cancelled.", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.ZLogError($"F# execution failed: {ex}");
            return ExecutionResult.Failed($"F# execution failed: {ex.Message}", ex, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<ScriptCompilationResult?> CompileAsync(
        string path,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(CompileTimeout);

        try
        {
            return await cache.GetOrCompileAsync(path, progress, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.ZLogError($"F# compilation timeout after {CompileTimeout.TotalSeconds:0}s for '{path}'.");
            return null;
        }
    }
}

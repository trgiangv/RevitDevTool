using System.Diagnostics;
using System.IO;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
namespace DevTools.Execution.Providers.FSharp;

/// <summary>
/// Execution strategy for F# scripts (.fsx).
/// Compiles via FSharpCompilationCache (with graph-level caching), executes via host context.
/// </summary>
public sealed class FSharpExecutionStrategy(
    string scriptPath,
    IHostContextExecutor hostContext,
    ICommandRunner commandRunner) : IExecutionStrategy
{
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(30);

    public async Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var scriptName = Path.GetFileName(scriptPath);
        try
        {
            progress?.Report($"Preparing {scriptName}...");
            var command = await CompileAsync(scriptPath, progress, cancellationToken).ConfigureAwait(false);
            if (command == null)
                return ExecutionResult.Failed($"F# compilation failed for '{scriptPath}'.", durationMs: stopwatch.ElapsedMilliseconds);

            progress?.Report($"Running {scriptName}...");
            var result = await hostContext
                .ExecuteAsync(() =>
                {
                    var execResult = FSharpExecutor.ExecuteCommand(command, commandRunner);
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
            Trace.TraceError($"F# execution failed: {ex}");
            return ExecutionResult.Failed($"F# execution failed: {ex.Message}", ex, stopwatch.ElapsedMilliseconds);
        }
    }

    private static async Task<object?> CompileAsync(
        string scriptPath,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(CompileTimeout);

        try
        {
            return await FSharpCompilationCache.GetOrCompileAsync(scriptPath, progress, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Trace.TraceError($"F# compilation timeout after {CompileTimeout.TotalSeconds:0}s for '{scriptPath}'.");
            return null;
        }
    }
}

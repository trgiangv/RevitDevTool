using System.Diagnostics;
using System.IO;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;

namespace DevTools.Execution.Providers.CSharp;

/// <summary>
/// Execution strategy for C# scripts (.csx).
/// Compiles via CSharpCompilationCache (with content-hash caching), executes via host context.
/// </summary>
public sealed class CSharpExecutionStrategy(
    string scriptPath,
    IHostContextExecutor hostContext,
    ICommandRunner commandRunner,
    IFSharpHostSupport hostSupport) : IExecutionStrategy
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
                return ExecutionResult.Failed($"C# compilation timeout after {CompileTimeout.TotalSeconds:0}s for '{scriptName}'.", durationMs: stopwatch.ElapsedMilliseconds);

            if (!compilationResult.Success || compilationResult.Command == null)
            {
                var errorMessage = compilationResult.Diagnostics.Count > 0
                    ? string.Join(Environment.NewLine, compilationResult.Diagnostics)
                    : $"C# compilation failed for '{scriptName}'.";
                return ExecutionResult.Failed(errorMessage, durationMs: stopwatch.ElapsedMilliseconds);
            }

            progress?.Report($"Running {scriptName}...");
            var result = await hostContext
                .ExecuteAsync(() =>
                {
                    var execResult = commandRunner.RunFSharpCommand(compilationResult.Command);
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
            return ExecutionResult.Cancelled("C# execution cancelled.", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Trace.TraceError($"C# execution failed: {ex}");
            return ExecutionResult.Failed($"C# execution failed: {ex.Message}", ex, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<CSharpCompilationResult?> CompileAsync(
        string scrPath,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(CompileTimeout);

        try
        {
            return await CSharpCompilationCache.GetOrCompileAsync(scrPath, hostSupport, progress, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Trace.TraceError($"C# compilation timeout after {CompileTimeout.TotalSeconds:0}s for '{scrPath}'.");
            return null;
        }
    }
}

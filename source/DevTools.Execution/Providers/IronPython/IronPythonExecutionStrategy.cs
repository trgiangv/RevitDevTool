using System.Diagnostics;
using System.IO;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Execution.Providers.IronPython;

/// <summary>Embedded IronPython 3.4.2 (DevTools stdlib + Trace logging).</summary>
public sealed class IronPythonExecutionStrategy(
    string scriptPath,
    string rootPath,
    IIronPythonBridge bridge,
    IHostContextExecutor hostContext,
    ILogger<IronPythonExecutionStrategy> logger) : IExecutionStrategy
{
    public async Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var scriptName = Path.GetFileName(scriptPath);
        try
        {
            progress?.Report($"Running IronPython {scriptName}...");
            var result = await hostContext
                .ExecuteAsync(() =>
                {
                    var run = IronPythonRunner.Execute(scriptPath, rootPath, bridge);
                    stopwatch.Stop();
                    return run.Success
                        ? ExecutionResult.Succeeded(run.Message, stopwatch.ElapsedMilliseconds)
                        : ExecutionResult.Failed(run.Message, run.Exception, stopwatch.ElapsedMilliseconds);
                }, cancellationToken)
                .ConfigureAwait(false);

            progress?.Report(result.Success ? $"Completed {scriptName}." : result.Message);
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return ExecutionResult.Cancelled("IronPython execution cancelled.", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.ZLogError($"IronPython execution pipeline failed: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return ExecutionResult.Failed($"IronPython execution pipeline failed: {ex.Message}", ex, stopwatch.ElapsedMilliseconds);
        }
    }
}

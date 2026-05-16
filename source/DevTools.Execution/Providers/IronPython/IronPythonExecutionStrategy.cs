using System.Diagnostics;
using System.IO;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;

namespace DevTools.Execution.Providers.IronPython;

public sealed class IronPythonExecutionStrategy(
    string scriptPath,
    string rootPath,
    IIronPythonBridge bridge,
    IHostContextExecutor hostContext)
    : IExecutionStrategy
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
                    var run = IronPythonScriptRunner.Execute(scriptPath, rootPath, bridge);
                    stopwatch.Stop();
                    return run.Success
                        ? ExecutionResult.Succeeded(run.Message, stopwatch.ElapsedMilliseconds)
                        : ExecutionResult.Failed(run.Message, run.Exception, stopwatch.ElapsedMilliseconds);
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
            return ExecutionResult.Cancelled("IronPython execution cancelled.", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Trace.TraceError($"IronPython execution pipeline failed: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return ExecutionResult.Failed($"IronPython execution pipeline failed: {ex.Message}", ex, stopwatch.ElapsedMilliseconds);
        }
    }
}

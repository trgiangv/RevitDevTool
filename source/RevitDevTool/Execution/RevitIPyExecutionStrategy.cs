using System.Diagnostics;
using System.IO;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.IronPython;
using RevitDevTool.Execution.PyRevit;

namespace RevitDevTool.Execution;

/// <summary>
/// Revit IronPython: pyRevit Labs <c>ScriptRuntime</c> (clean engine) when loaded, otherwise embedded IPy 3.4.2.
/// </summary>
public sealed class RevitIPyExecutionStrategy(
    string scriptPath,
    string rootPath,
    IIronPythonBridge bridge,
    IHostContextExecutor hostContext)
    : IExecutionStrategy
{
    private readonly IronPythonExecutionStrategy _native =
        new(scriptPath, rootPath, bridge, hostContext);

    public Task<ExecutionResult> ExecuteAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default) =>
        PyRevitLibraryPaths.IsLoaded
            ? ExecutePyrevitAsync(progress, cancellationToken)
            : _native.ExecuteAsync(progress, cancellationToken);

    private async Task<ExecutionResult> ExecutePyrevitAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var scriptName = Path.GetFileName(scriptPath);
        try
        {
            progress?.Report($"Running IronPython (pyRevit) {scriptName}...");
            var result = await hostContext
                .ExecuteAsync(() =>
                {
                    var run = PyRevitScriptExecutor.Execute(scriptPath, rootPath);
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
            Trace.TraceError($"pyRevit execution pipeline failed: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return ExecutionResult.Failed($"pyRevit execution pipeline failed: {ex.Message}", ex, stopwatch.ElapsedMilliseconds);
        }
    }
}

using System.Diagnostics;
using System.IO;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.IronPython;
using Microsoft.Extensions.Logging;
using RevitDevTool.Execution.PyRevit;
using ZLogger;

namespace RevitDevTool.Execution;

/// <summary>
/// Revit IronPython: pyRevit Labs <c>ScriptRuntime</c> when loaded and no
/// pydevd client is attached; otherwise the embedded IPy 3.4.2 session engine.
/// </summary>
public sealed class RevitIPyExecutionStrategy(
    string scriptPath,
    string rootPath,
    IIronPythonBridge bridge,
    IHostContextExecutor hostContext,
    ILogger<IronPythonExecutionStrategy> ironPythonLogger,
    ILogger<RevitIPyExecutionStrategy> logger,
    IronPythonDebugger ironPythonDebugger)
    : IExecutionStrategy
{
    private readonly IronPythonExecutionStrategy _native =
        new(scriptPath, rootPath, bridge, hostContext, ironPythonLogger, ironPythonDebugger);

    public Task<ExecutionResult> ExecuteAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // 0026: unattached Run stays pyRevit-first. pydevd lives on the
        // session engine (Frames), not ScriptExecutor (full_frame: false).
        // When VS Code is attached, yield so *_ipy_script.py can hit.
        if (PyRevitLibraryPaths.IsLoaded && !ironPythonDebugger.IsAttached)
            return ExecutePyrevitAsync(progress, cancellationToken);

        if (PyRevitLibraryPaths.IsLoaded)
        {
            logger.ZLogInformation(
                $"IronPython debugger attached; running '{Path.GetFileName(scriptPath)}' on the embedded engine (not pyRevit).");
        }

        return _native.ExecuteAsync(progress, cancellationToken);
    }

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
                    var run = PyRevitScriptExecutor.Execute(scriptPath, rootPath, logger);
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
            logger.ZLogError($"pyRevit execution pipeline failed: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return ExecutionResult.Failed($"pyRevit execution pipeline failed: {ex.Message}", ex, stopwatch.ElapsedMilliseconds);
        }
    }
}

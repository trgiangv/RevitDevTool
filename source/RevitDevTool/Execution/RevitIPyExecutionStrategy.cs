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
/// pyRevit loaded → ScriptExecutor / PyRevitLoader on that engine's
/// <see cref="IronPythonDebugger"/>. Otherwise embedded IronPython 3.4.2.
/// </summary>
public sealed class RevitIPyExecutionStrategy(
    string scriptPath,
    string rootPath,
    IIronPythonBridge bridge,
    IHostContextExecutor hostContext,
    ILogger<IronPythonExecutionStrategy> ironPythonLogger,
    ILogger<RevitIPyExecutionStrategy> logger,
    IronPythonInitializer ironPythonInitializer)
    : IExecutionStrategy
{
    private readonly IronPythonExecutionStrategy _native =
        new(scriptPath, rootPath, ironPythonInitializer, bridge, hostContext, ironPythonLogger);

    public Task<ExecutionResult> ExecuteAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return !PyRevitLibraryPaths.IsLoaded 
            ? _native.ExecuteAsync(progress, cancellationToken) 
            : ExecutePyrevitAsync(progress, cancellationToken);
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
                    IronPythonDebugger.RefreshUserModules(
                        ironPythonInitializer.Engine,
                        scriptPath,
                        PyRevitExtensionPaths.ModuleRefreshRoot(scriptPath),
                        PyRevitLibraryPaths.RefreshSkipRoots,
                        logger);
                    PyRevitReflectionCache.Instance.EnsureHostAppImported();
                    IronPythonDebugger.EnsureCurrentThreadTraced(ironPythonInitializer.Engine, logger);

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

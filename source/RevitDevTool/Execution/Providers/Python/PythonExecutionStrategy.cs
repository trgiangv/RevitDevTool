using System.Diagnostics;
using System.IO;
using Python.Runtime;
using RevitDevTool.Controllers;
using RevitDevTool.Execution.Interfaces;
using RevitDevTool.Execution.Models;
namespace RevitDevTool.Execution.Providers.Python;

/// <summary>
/// Execution strategy for Python scripts.
/// Handles dependency resolution and execution orchestration directly.
/// </summary>
public sealed class PythonExecutionStrategy(string scriptPath, string rootPath) : IExecutionStrategy
{
    public async Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var scriptName = Path.GetFileName(scriptPath);
        try
        {
            progress?.Report($"Initializing {scriptName}...");
            await PythonBootstrap.EnsureExecutorReadyAsync(cancellationToken).ConfigureAwait(false);

            string scriptContent;
            try
            {
#if NETCOREAPP
                scriptContent = await File.ReadAllTextAsync(scriptPath, cancellationToken).ConfigureAwait(false);
#else
                scriptContent = await Task.Run(() => File.ReadAllText(scriptPath), cancellationToken).ConfigureAwait(false);
#endif
            }
            catch (IOException ex)
            {
                Trace.TraceError($"Failed to read script file: {ex.Message}");
                throw;
            }

            var success = await ResolveDependenciesAsync(scriptPath, progress, cancellationToken).ConfigureAwait(false);
            if (!success)
            {
                stopwatch.Stop();
                return ExecutionResult.Failed("Dependency resolution failed.", durationMs: stopwatch.ElapsedMilliseconds);
            }

            progress?.Report($"Running {scriptName}...");
            var handler = await ExternalEventController
                .AsyncGenericEventHandler<ExecutionResult>()
                .ConfigureAwait(false);

            var result = await handler
                .RaiseAsync(_ =>
                {
                    PythonExecutor.ExecuteScript(scriptPath, scriptContent, rootPath);
                    stopwatch.Stop();
                    return ExecutionResult.Succeeded("Python script completed successfully.", stopwatch.ElapsedMilliseconds);
                })
                .ConfigureAwait(false);

            progress?.Report(result.Success
                ? $"Completed {scriptName}."
                : result.Message);

            return result;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            stopwatch.Stop();
            return ExecutionResult.Cancelled("Python execution cancelled.", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Trace.TraceError($"Python execution pipeline failed: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return ExecutionResult.Failed($"Python execution pipeline failed: {ex.Message}", ex, stopwatch.ElapsedMilliseconds);
        }
    }

    private static async Task<bool> ResolveDependenciesAsync(
        string scriptPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Parse PEP 723 metadata
        List<string> dependencies;
        try
        {
            progress?.Report("Resolving Python dependencies...");
            dependencies = await PythonDepsManager.ResolveDependenciesAsync(scriptPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to parse PEP 723 metadata: {ex.Message}");
            return false;
        }

        // 2. No dependencies or empty list -> success
        if (dependencies.Count == 0)
            return true;

        var reporter = progress ?? new Progress<string>(_ => { });
        Trace.TraceInformation($"Installing {dependencies.Count} dependency(s) via pixi...");
        reporter.Report($"Installing {dependencies.Count} dependency(s) via pixi...");
        await PythonDepsManager.InstallDependenciesAsync(dependencies, reporter, cancellationToken).ConfigureAwait(false);
        
        RefreshImportCache();
        return true;
    }

    /// <summary>
    /// After uv installs new packages on disk, Python's PathFinder caches
    /// the directory listings from sys.path entries. invalidate_caches()
    /// forces it to rescan on the next import.
    /// </summary>
    private static void RefreshImportCache()
    {
        if (!PythonInitializer.IsInitialized) return;
        
        try
        {
            using (Py.GIL())
            {
                using var scope = Py.CreateScope();
                scope.Exec("""
                            import importlib
                            import site
                            import sys
                            
                            importlib.invalidate_caches()
                            
                            for sp in site.getsitepackages():
                                if sp not in sys.path:
                                    sys.path.insert(0, sp)
                            
                            user_site = site.getusersitepackages()
                            if isinstance(user_site, str) and user_site not in sys.path:
                                sys.path.insert(0, user_site)
                            """);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to refresh Python import cache: {ex.Message}");
        }
    }

}
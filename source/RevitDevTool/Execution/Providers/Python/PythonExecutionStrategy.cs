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
public sealed class PythonExecutionStrategy(string scriptPath, string rootPath, PythonInitializer pythonInitializer)
    : IExecutionStrategy
{
    public async Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var scriptName = Path.GetFileName(scriptPath);
        try
        {
            progress?.Report($"Initializing {scriptName}...");
            await pythonInitializer.InitializeAsync().ConfigureAwait(true);

            var scriptContent = await File.ReadAllTextAsync(scriptPath, cancellationToken).ConfigureAwait(false);

            var success = await ResolveDependenciesAsync(pythonInitializer, scriptPath, progress, cancellationToken).ConfigureAwait(false);
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
                .RaiseAsync(() =>
                {
                    PythonExecutor.ExecuteScript(pythonInitializer, scriptPath, scriptContent, rootPath);
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

    public static async Task<bool> ResolveDependenciesAsync(
        PythonInitializer pythonInitializer,
        string scriptPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var provider = pythonInitializer.Provider
            ?? throw new InvalidOperationException("Python environment provider not initialized.");

        List<string> dependencies;
        try
        {
            progress?.Report("Resolving Python dependencies...");
            dependencies = await PythonDepsManager.ResolveDependenciesAsync(provider, scriptPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to parse PEP 723 metadata: {ex.Message}");
            return false;
        }

        if (dependencies.Count == 0)
            return true;

        var reporter = progress ?? new Progress<string>(_ => { });
        Trace.TraceInformation($"Installing {dependencies.Count} dependency(s) via {provider.Backend}...");
        reporter.Report($"Installing {dependencies.Count} dependency(s) via {provider.Backend}...");
        await PythonDepsManager.InstallDependenciesAsync(provider, dependencies, reporter, cancellationToken).ConfigureAwait(false);
        
        RefreshImportCache(pythonInitializer);
        return true;
    }

    /// <summary>
    /// After uv installs new packages on disk, Python's PathFinder caches
    /// the directory listings from sys.path entries. invalidate_caches()
    /// forces it to rescan on the next import.
    /// </summary>
    private static void RefreshImportCache(PythonInitializer pythonInitializer)
    {
        if (!pythonInitializer.IsInitialized) return;
        
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
using System.Diagnostics;
using System.IO;
using Python.Runtime;
using RevitDevTool.Core;
using RevitDevTool.Execution.Interfaces;
using RevitDevTool.Execution.Models;
namespace RevitDevTool.Execution.Providers.Python;

/// <summary>
/// Execution strategy for Python scripts.
/// Handles dependency resolution and execution orchestration directly.
/// </summary>
public sealed class PythonExecutionStrategy(
    string scriptPath,
    string rootPath,
    PythonInitializer pythonInitializer,
    PythonExecutor executor)
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
            var result = await RevitContextExecutor
                .RaiseAsync(() =>
                {
                    executor.Execute(
                        scriptPath,
                        rootPath,
                        scope =>
                        {
                            scope.Set(PythonInstances.Source, new PyString(scriptContent));
                            scope.Exec("""
                                       compiled_code = compile(__source__, __file__, 'exec')
                                       exec(compiled_code, globals())
                                       """);
                            return 0;
                        });
                    stopwatch.Stop();
                    return ExecutionResult.Succeeded("Python script completed successfully.", stopwatch.ElapsedMilliseconds);
                }, cancellationToken)
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
    /// After installs new packages on disk, Python's PathFinder caches
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
                            import os
                            import sys
                             
                            importlib.invalidate_caches()

                            site_packages = os.path.join(sys.prefix, "Lib", "site-packages")
                            if os.path.isdir(site_packages) and site_packages not in sys.path:
                                sys.path.insert(0, site_packages)
                            """);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to refresh Python import cache: {ex.Message}");
        }
    }

}

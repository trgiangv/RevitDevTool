using System.Diagnostics;
using System.IO;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using Microsoft.Extensions.Logging;
using Python.Runtime;
using ZLogger;

namespace DevTools.Execution.Providers.Python;

public sealed class PythonExecutionStrategy(
    string scriptPath,
    string rootPath,
    PythonInitializer pythonInitializer,
    PythonExecutor executor,
    IHostContextExecutor hostContext,
    ILogger<PythonExecutionStrategy> logger)
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

            var success = await ResolveDependenciesAsync(pythonInitializer, scriptPath, progress, cancellationToken, logger).ConfigureAwait(false);
            if (!success)
            {
                stopwatch.Stop();
                return ExecutionResult.Failed("Dependency resolution failed.", durationMs: stopwatch.ElapsedMilliseconds);
            }

            progress?.Report($"Running {scriptName}...");
            var result = await hostContext
                .ExecuteAsync(() =>
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
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return ExecutionResult.Cancelled("Python execution cancelled.", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.ZLogError($"Python execution pipeline failed: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return ExecutionResult.Failed($"Python execution pipeline failed: {ex.Message}", ex, stopwatch.ElapsedMilliseconds);
        }
    }

    public static async Task<bool> ResolveDependenciesAsync(
        PythonInitializer pythonInitializer,
        string scriptPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        ILogger? logger = null)
    {
        if (pythonInitializer.Provider is not { } provider)
            return true;

        List<string> dependencies;
        try
        {
            progress?.Report("Resolving Python dependencies...");
            dependencies = await PythonDepsManager.ResolveDependenciesAsync(provider, scriptPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.ZLogError($"Failed to parse PEP 723 metadata: {ex.Message}");
            return false;
        }

        if (dependencies.Count == 0)
            return true;

        var reporter = progress ?? new Progress<string>(_ => { });
        logger?.ZLogInformation($"Installing {dependencies.Count} dependency(s) via {provider.Backend}...");
        reporter.Report($"Installing {dependencies.Count} dependency(s) via {provider.Backend}...");
        await PythonDepsManager.InstallDependenciesAsync(provider, dependencies, reporter, cancellationToken).ConfigureAwait(false);

        try
        {
            PythonDepsManager.RefreshImportCache(pythonInitializer);
        }
        catch (Exception ex)
        {
            logger?.ZLogWarning($"Failed to refresh Python import cache: {ex.Message}");
        }

        return true;
    }
}

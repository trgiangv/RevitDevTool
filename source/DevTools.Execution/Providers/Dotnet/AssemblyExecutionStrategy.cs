using System.Diagnostics;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using Microsoft.Extensions.Logging;
using ZLogger;
namespace DevTools.Execution.Providers.Dotnet;

/// <summary>
/// Execution strategy for .NET commands.
/// </summary>
public sealed class AssemblyExecutionStrategy(
    CommandItem commandItem,
    IHostContextExecutor hostContext,
    ICommandRunner commandRunner,
    ILogger<AssemblyExecutionStrategy> logger) : IExecutionStrategy
{
    public async Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            progress?.Report($"Running {commandItem.Name}...");

            var result = await hostContext
                .ExecuteAsync(() =>
                {
                    var execResult = commandRunner.RunCommand(commandItem);
                    stopwatch.Stop();
                    return execResult;
                }, cancellationToken)
                .ConfigureAwait(false);

            progress?.Report(result.Success
                ? $"Completed {commandItem.Name}."
                : result.Message);

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return ExecutionResult.Cancelled("Dotnet execution cancelled.", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.ZLogError($"Dotnet execution failed: {ex}");
            return ExecutionResult.Failed($"Dotnet execution failed: {ex.Message}", ex, stopwatch.ElapsedMilliseconds);
        }
    }
}

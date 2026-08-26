using Microsoft.Extensions.Logging;
using RevitDevTool.Core;
using RevitDevTool.Core.Execution;
namespace RevitDevTool.Adapters;

public sealed class RevitHostContextExecutor(
    IExecutionGuard executionGuard,
    ILogger<RevitHostContextExecutor> logger) : IHostContextExecutor
{
    public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default)
    {
        var mode = ExecutionGuardContext.Mode;
        return RevitContextExecutor.RaiseAsync(() =>
        {
            using var scope = executionGuard.Begin(mode);
            var result = handler();
            PublishGuardResult();
            return result;
        }, token);
    }

    public Task ExecuteAsync(Action action, CancellationToken token = default)
    {
        var mode = ExecutionGuardContext.Mode;
        return RevitContextExecutor.RaiseAsync(() =>
        {
            using var scope = executionGuard.Begin(mode);
            action();
            PublishGuardResult();
        }, token);
    }

    private void PublishGuardResult()
    {
        if (executionGuard.HadRollback)
        {
            var summary = executionGuard.RollbackSummary;
            ExecutionGuardContext.RollbackSummary = summary;
            logger.LogWarning("[ExecutionGuard] {RollbackSummary}", summary);
        }

        var logSummary = executionGuard.LastLogSummary;
        if (!string.IsNullOrEmpty(logSummary))
            logger.LogDebug("[ExecutionGuard] {Summary}", logSummary);
    }
}

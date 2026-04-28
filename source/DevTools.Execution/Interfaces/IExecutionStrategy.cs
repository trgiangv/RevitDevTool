using DevTools.Execution.Models;
namespace DevTools.Execution.Interfaces;

/// <summary>
/// Strategy interface for executing different types of code.
/// Implements Strategy Pattern for polymorphic execution.
/// Execution is dispatched via IHostContextExecutor.
/// </summary>
public interface IExecutionStrategy
{
    /// <summary>
    /// Execute the code/script via the host context executor.
    /// </summary>
    /// <param name="progress">Optional progress reporter for status updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}
namespace DevTools.Execution.Abstractions;

/// <summary>
/// Dispatches delegates to the host application's API thread.
/// </summary>
public interface IHostContextExecutor
{
    Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default);
    Task ExecuteAsync(Action action, CancellationToken token = default);
}

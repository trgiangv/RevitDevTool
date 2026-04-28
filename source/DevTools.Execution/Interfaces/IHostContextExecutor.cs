namespace DevTools.Execution.Interfaces;

/// <summary>
/// Dispatches delegates to the host application's API thread.
/// Revit uses ExternalEvent, AutoCAD uses Document.LockDocument(), etc.
/// </summary>
public interface IHostContextExecutor
{
    Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default);
    Task ExecuteAsync(Action action, CancellationToken token = default);
    Task<T> ExecuteAsync<T>(Func<Task<T>> asyncHandler, CancellationToken token = default);
}

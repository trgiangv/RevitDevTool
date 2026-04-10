using System.ComponentModel;
// ReSharper disable once CheckNamespace
namespace RevitDevTool.Core;

/// <summary>
///     Handler, to provide access to modify the Revit document.
/// </summary>
/// <remarks>Suitable for cases where it is needed to await the completion of an external event with the return of a value.</remarks>
[PublicAPI]
public sealed class AsyncEventHandler<T> : ExternalEventHandler
{
    private Func<UIApplication, T>? _contextDelegate;
    private Func<T>? _delegate;
    private Func<Task<T>>? _asyncDelegate;
    private TaskCompletionSource<T>? _resultTask;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    ///     This method is called to handle the external event.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void Execute(UIApplication uiApplication)
    {
        if (_contextDelegate is null && _delegate is null && _asyncDelegate is null) return;
        if (_resultTask is null) return;

        try
        {
            if (_contextDelegate != null)
            {
                var result = _contextDelegate(uiApplication);
                _resultTask.SetResult(result);
            }
            else if (_asyncDelegate != null)
            {
                // Clear SynchronizationContext before blocking to prevent deadlock:
                // continuations inside the async delegate cannot marshal back to this thread.
                var prevCtx = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(null);
                try
                {
                    var result = _asyncDelegate().GetAwaiter().GetResult();
                    _resultTask.SetResult(result);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(prevCtx);
                }
            }
            else if (_delegate != null)
            {
                var result = _delegate();
                _resultTask.SetResult(result);
            }
        }
        catch (Exception exception)
        {
            _resultTask.SetException(exception);
        }
        finally
        {
            _contextDelegate = null;
            _asyncDelegate = null;
            _resultTask = null;
        }
    }

    /// <summary>
    ///     Instructing Revit to queue a handler, raise (signal) the external event and async awaiting for its completion.
    /// </summary>
    /// <returns>
    ///     The return value of the method that delegate encapsulates.
    /// </returns>
    /// <remarks>
    ///     This method async awaiting completion of the <see cref="AsyncEventHandler.Execute" /> method. <br />
    ///     Exceptions in the delegate will not be ignored and will be rethrown in the original synchronization context.<br />
    ///     <see cref="System.Threading.Tasks.Task.WaitAll(System.Threading.Tasks.Task[])" />,
    ///     <see cref="System.Threading.Tasks.Task.Wait()" /> will cause a deadlock.<br/><br/>
    ///     Executes the handler out of queue if Revit is in API mode.
    /// </remarks>
    public async Task<T> RaiseAsync(Func<UIApplication, T> handler)
    {
        if (RevitContext.IsRevitInApiMode)
        {
            return handler(RevitContext.UiApplication)!;
        }
        
        await _semaphore.WaitAsync();

        try
        {
            _contextDelegate = handler;
            _resultTask = new TaskCompletionSource<T>();
        
            Raise();
            return await _resultTask.Task;
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    /// <summary>
    ///     Instructing Revit to queue a handler, raise (signal) the external event and async awaiting for its completion.
    /// </summary>
    /// <returns>
    ///     The return value of the method that delegate encapsulates.
    /// </returns>
    /// <remarks>
    ///     This method async awaiting completion of the <see cref="AsyncEventHandler.Execute" /> method. <br />
    ///     Exceptions in the delegate will not be ignored and will be rethrown in the original synchronization context.<br />
    ///     <see cref="System.Threading.Tasks.Task.WaitAll(System.Threading.Tasks.Task[])" />,
    ///     <see cref="System.Threading.Tasks.Task.Wait()" /> will cause a deadlock.<br/><br/>
    ///     Executes the handler out of queue if Revit is in API mode.
    /// </remarks>
    public async Task<T> RaiseAsync(Func<T> handler)
    {
        if (RevitContext.IsRevitInApiMode)
        {
            return handler();
        }

        await _semaphore.WaitAsync();

        try
        {
            _delegate = handler;
            _resultTask = new TaskCompletionSource<T>();

            Raise();
            return await _resultTask.Task;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    ///     Instructing Revit to queue a handler, raise (signal) the external event and async awaiting for its completion.
    /// </summary>
    /// <returns>
    ///     The return value of the method that delegate encapsulates.
    /// </returns>
    /// <remarks>
    ///     The <see cref="SynchronizationContext"/> is cleared before invoking the async delegate inside
    ///     <see cref="Execute"/> to prevent deadlocks: continuations cannot marshal back to the blocked Revit main thread.<br /><br />
    ///     Executes the handler out of queue if Revit is in API mode.
    /// </remarks>
    public async Task<T> RaiseAsync(Func<Task<T>> asyncHandler)
    {
        if (RevitContext.IsRevitInApiMode)
            return await asyncHandler().ConfigureAwait(false);

        await _semaphore.WaitAsync();
        try
        {
            _asyncDelegate = asyncHandler;
            _resultTask = new TaskCompletionSource<T>();
            Raise();
            return await _resultTask.Task;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
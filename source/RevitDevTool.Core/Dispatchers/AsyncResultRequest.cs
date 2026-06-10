namespace RevitDevTool.Core.Dispatchers;

/// <summary>Awaitable request wrapping an async function that returns <typeparamref name="T"/>.</summary>
internal sealed class AsyncResultRequest<T> : IRevitRequest
{
    private readonly Func<UIApplication, Task<T>> _asyncHandler;
    private readonly TaskCompletionSource<T> _completionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenRegistration _registration;

    public AsyncResultRequest(Func<Task<T>> asyncHandler, CancellationToken token = default)
        : this(_ => asyncHandler(), token)
    {
    }

    public AsyncResultRequest(Func<UIApplication, Task<T>> asyncHandler, CancellationToken token = default)
    {
        _asyncHandler = asyncHandler;
        if (token.CanBeCanceled)
            _registration = token.Register(() => _completionSource.TrySetCanceled(token));
    }

    public Task<T> Task => _completionSource.Task;

    public void Execute(UIApplication uiApplication)
    {
        if (_completionSource.Task.IsCanceled)
        {
            DisposeRegistration();
            return;
        }

        try
        {
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                _asyncHandler(uiApplication).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        _completionSource.TrySetException(t.Exception!.InnerExceptions);
                    else if (t.IsCanceled)
                        _completionSource.TrySetCanceled();
                    else
                        _completionSource.TrySetResult(t.Result);

                    DisposeRegistration();
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }
        catch (Exception exception)
        {
            _completionSource.TrySetException(exception);
            DisposeRegistration();
        }
    }

    public void Fail(Exception exception)
    {
        _completionSource.TrySetException(exception);
        DisposeRegistration();
    }

    private void DisposeRegistration()
    {
        _registration.Dispose();
    }
}

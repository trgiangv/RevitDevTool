namespace RevitDevTool.Core.Dispatchers;

/// <summary>Awaitable request wrapping a synchronous or async action delegate.</summary>
internal sealed class AsyncRequest : IRevitRequest
{
    private readonly Action<UIApplication>? _action;
    private readonly Func<Task>? _asyncAction;
    private readonly TaskCompletionSource<bool> _completionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenRegistration _registration;

    public AsyncRequest(Action<UIApplication> action, CancellationToken token = default)
    {
        _action = action;
        if (token.CanBeCanceled)
            _registration = token.Register(() => _completionSource.TrySetCanceled(token));
    }

    public AsyncRequest(Func<Task> asyncAction, CancellationToken token = default)
    {
        _asyncAction = asyncAction;
        if (token.CanBeCanceled)
            _registration = token.Register(() => _completionSource.TrySetCanceled(token));
    }

    public Task Task => _completionSource.Task;

    public void Execute(UIApplication uiApplication)
    {
        if (_completionSource.Task.IsCanceled)
        {
            DisposeRegistration();
            return;
        }

        if (_asyncAction is not null)
        {
            ExecuteAsync();
            return;
        }

        try
        {
            _action!(uiApplication);
            _completionSource.TrySetResult(true);
        }
        catch (Exception exception)
        {
            _completionSource.TrySetException(exception);
        }
        finally
        {
            DisposeRegistration();
        }
    }

    private void ExecuteAsync()
    {
        try
        {
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                _asyncAction!().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        _completionSource.TrySetException(t.Exception!.InnerExceptions);
                    else if (t.IsCanceled)
                        _completionSource.TrySetCanceled();
                    else
                        _completionSource.TrySetResult(true);

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

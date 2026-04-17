namespace RevitDevTool.Core.Handlers;

/// <summary>Awaitable request wrapping a synchronous function that returns <typeparamref name="T"/>.</summary>
internal sealed class ResultRequest<T> : IRevitRequest
{
    private readonly Func<UIApplication, T> _handler;
    private readonly TaskCompletionSource<T> _completionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration _registration;

    public ResultRequest(Func<UIApplication, T> handler, CancellationToken token = default)
    {
        _handler = handler;
        if (token.CanBeCanceled)
            _registration = token.Register(() => _completionSource.TrySetCanceled(token));
    }

    public Task<T> Task => _completionSource.Task;

    public void Execute(UIApplication uiApplication)
    {
        if (_completionSource.Task.IsCanceled) return;
        try
        {
            var result = _handler(uiApplication);
            _completionSource.TrySetResult(result);
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

    private void DisposeRegistration()
    {
        _registration.Dispose();
    }
}

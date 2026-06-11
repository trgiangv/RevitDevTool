namespace RevitDevTool.Core.Dispatchers;

/// <summary>Awaitable request wrapping a synchronous Revit API action delegate.</summary>
internal sealed class ActionRequest : IRevitRequest
{
    private readonly Action<UIApplication> _action;
    private readonly TaskCompletionSource<bool> _completionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration _registration;

    public ActionRequest(Action<UIApplication> action, CancellationToken token = default)
    {
        _action = action;
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

        try
        {
            _action(uiApplication);
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

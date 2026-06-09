namespace RevitDevTool.Core.Handlers;

/// <summary>
///     Manages a FIFO queue of <see cref="IRevitRequest"/> items dispatched via
///     <see cref="Autodesk.Revit.UI.ExternalEvent"/>. Callers enqueue work through this dispatcher;
///     the dispatcher raises the external event, and Revit invokes
///     <see cref="Autodesk.Revit.UI.IExternalEventHandler.Execute"/> on the main thread in a valid API context.
///     A <c>while</c> loop inside <c>Execute</c> drains items added during processing,
///     maximizing throughput through natural batching.
/// </summary>
internal sealed class RevitDispatcher : IExternalEventHandler
{
    private readonly Lock _gate = new();
    private bool _raisePending;
    private readonly Queue<IRevitRequest> _queue = new();
    private readonly ExternalEvent? _event;

    public RevitDispatcher()
    {
        using (RevitContext.BeginApiContextScope())
        {
            _event ??= ExternalEvent.Create(this);
        }
    }

    public void Execute(UIApplication app)
    {
        while (true)
        {
            IRevitRequest[] batch;

            lock (_gate)
            {
                if (_queue.Count == 0)
                {
                    _raisePending = false;
                    return;
                }
                batch = _queue.ToArray();
                _queue.Clear();
            }

            foreach (var request in batch)
                request.Execute(app);
        }
    }

    string IExternalEventHandler.GetName() => nameof(RevitDispatcher);

    public void Post(Action<UIApplication> action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        if (AllowDirectInvocation())
        {
            action(RevitContext.UiApplication);
            return;
        }

        Enqueue(new Request(action));
    }

    public void Post(Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        if (AllowDirectInvocation())
        {
            action();
            return;
        }

        Enqueue(new Request(_ => action()));
    }

    public Task InvokeAsync(Action<UIApplication> action, CancellationToken token = default)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        if (AllowDirectInvocation())
        {
            try
            {
                token.ThrowIfCancellationRequested();
                action(RevitContext.UiApplication);
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                return Task.FromException(exception);
            }
        }

        var request = new AsyncRequest(action, token);
        Enqueue(request);
        return request.Task;
    }

    public Task InvokeAsync(Action action, CancellationToken token = default)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        if (AllowDirectInvocation())
        {
            try
            {
                token.ThrowIfCancellationRequested();
                action();
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                return Task.FromException(exception);
            }
        }

        var request = new AsyncRequest(_ => action(), token);
        Enqueue(request);
        return request.Task;
    }

    public Task<T> InvokeAsync<T>(Func<UIApplication, T> handler, CancellationToken token = default)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        if (AllowDirectInvocation())
        {
            try
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(handler(RevitContext.UiApplication));
            }
            catch (Exception exception)
            {
                return Task.FromException<T>(exception);
            }
        }

        var request = new ResultRequest<T>(handler, token);
        Enqueue(request);
        return request.Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> handler, CancellationToken token = default)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        if (AllowDirectInvocation())
        {
            try
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(handler());
            }
            catch (Exception exception)
            {
                return Task.FromException<T>(exception);
            }
        }

        var request = new ResultRequest<T>(_ => handler(), token);
        Enqueue(request);
        return request.Task;
    }

    public Task<T> InvokeAsync<T>(Func<Task<T>> asyncHandler, CancellationToken token = default)
    {
        if (asyncHandler is null) throw new ArgumentNullException(nameof(asyncHandler));

        if (AllowDirectInvocation())
        {
            token.ThrowIfCancellationRequested();
            return asyncHandler();
        }

        var request = new AsyncResultRequest<T>(asyncHandler, token);
        Enqueue(request);
        return request.Task;
    }

    public Task<T> InvokeAsync<T>(Func<UIApplication, Task<T>> asyncHandler, CancellationToken token = default)
    {
        if (asyncHandler is null) throw new ArgumentNullException(nameof(asyncHandler));

        if (AllowDirectInvocation())
        {
            token.ThrowIfCancellationRequested();
            return asyncHandler(RevitContext.UiApplication);
        }

        var request = new AsyncResultRequest<T>(asyncHandler, token);
        Enqueue(request);
        return request.Task;
    }

    public Task InvokeAsync(Func<Task> asyncAction, CancellationToken token = default)
    {
        if (asyncAction is null) throw new ArgumentNullException(nameof(asyncAction));

        if (AllowDirectInvocation())
        {
            token.ThrowIfCancellationRequested();
            return asyncAction();
        }

        var request = new AsyncRequest(asyncAction, token);
        Enqueue(request);
        return request.Task;
    }

    /// <returns>
    ///     <see langword="true"/> when the caller is on the Revit thread in API mode
    ///     and no requests are queued, allowing direct synchronous execution.
    /// </returns>
    private bool AllowDirectInvocation()
    {
        if (!RevitContext.IsRevitInApiMode) return false;

        lock (_gate)
        {
            return _queue.Count == 0 && !_raisePending;
        }
    }

    private void Enqueue(IRevitRequest request)
    {
        lock (_gate)
        {
            _queue.Enqueue(request);
            if (_raisePending) return;
            _raisePending = true;
        }

        _event!.Raise();
    }
}

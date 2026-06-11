namespace RevitDevTool.Core.Dispatchers;

/// <summary>
///     Manages a FIFO queue of <see cref="IRevitRequest"/> items dispatched via
///     <see cref="Autodesk.Revit.UI.ExternalEvent"/>. Callers enqueue work through this dispatcher;
///     the dispatcher raises the external event, and Revit invokes
///     <see cref="Autodesk.Revit.UI.IExternalEventHandler.Execute"/> on the main thread in a valid API context.
///     A <c>while</c> loop inside <c>Execute</c> drains items added during processing,
///     maximizing throughput through natural batching.
/// </summary>
internal sealed class RevitDispatcher : IExternalEventHandler, IRevitDispatcher, IDisposable
{
    private readonly Lock _gate = new();
    private readonly Queue<IRevitRequest> _queue = new();
    private readonly ExternalEvent? _event;
    private bool _raisePending;
    private int _disposed;

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

        var request = new Request(action);

        if (AllowDirectInvocation())
        {
            request.Execute(RevitContext.UiApplication);
            return;
        }

        Enqueue(request);
    }

    public void Post(Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        var request = new Request(_ => action());

        if (AllowDirectInvocation())
        {
            request.Execute(RevitContext.UiApplication);
            return;
        }

        Enqueue(request);
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

        var request = new ActionRequest(action, token);
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

        var request = new ActionRequest(_ => action(), token);
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

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        IRevitRequest[] pending;
        lock (_gate)
        {
            pending = _queue.ToArray();
            _queue.Clear();
            _raisePending = false;
        }

        var exception = new ObjectDisposedException(nameof(RevitDispatcher));
        foreach (var request in pending)
            request.Fail(exception);

        _event?.Dispose();
    }

    /// <returns>
    ///     <see langword="true"/> when the caller is on the Revit thread in API mode
    ///     and no requests are queued, allowing direct synchronous execution.
    /// </returns>
    private bool AllowDirectInvocation()
    {
        if (_disposed != 0) return false;
        if (!RevitContext.IsRevitInApiMode) return false;

        lock (_gate)
        {
            return _queue.Count == 0 && !_raisePending;
        }
    }

    private void Enqueue(IRevitRequest request)
    {
        var shouldRaise = false;

        lock (_gate)
        {
            if (_disposed != 0)
            {
                request.Fail(new ObjectDisposedException(nameof(RevitDispatcher)));
                return;
            }

            _queue.Enqueue(request);

            if (!_raisePending)
            {
                _raisePending = true;
                shouldRaise = true;
            }
        }

        if (!shouldRaise)
            return;

        RaiseExternalEvent();
    }

    private void RaiseExternalEvent()
    {
        try
        {
            var request = _event!.Raise();
            if (IsAcceptedRequest(request))
                return;

            FailPendingRequests(new InvalidOperationException(
                $"ExternalEvent.Raise was not accepted. Request status: {request}."));
        }
        catch (Exception exception)
        {
            FailPendingRequests(exception);
        }
    }

    private static bool IsAcceptedRequest(ExternalEventRequest request)
    {
        return request is ExternalEventRequest.Accepted or ExternalEventRequest.Pending;
    }

    private void FailPendingRequests(Exception exception)
    {
        IRevitRequest[] pending;

        lock (_gate)
        {
            pending = _queue.ToArray();
            _queue.Clear();
            _raisePending = false;
        }

        foreach (var request in pending)
            request.Fail(exception);
    }
}

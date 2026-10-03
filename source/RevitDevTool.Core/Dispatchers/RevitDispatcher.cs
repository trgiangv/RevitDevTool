namespace RevitDevTool.Core.Dispatchers;

/// <summary>
/// FIFO queue drained on Revit's main thread through one <see cref="Autodesk.Revit.UI.ExternalEvent"/>.
/// <see cref="Autodesk.Revit.UI.ExternalEvent.Raise"/> is a single signal:
/// <see cref="Autodesk.Revit.UI.ExternalEventRequest.Accepted"/> and
/// <see cref="Autodesk.Revit.UI.ExternalEventRequest.Pending"/> both mean a turn is already scheduled.
/// <see cref="Execute"/> drains the queue; if items remain when it leaves, it raises once more.
/// </summary>
internal sealed class RevitDispatcher : IExternalEventHandler, IRevitDispatcher, IDisposable
{
    private readonly Lock _gate = new();
    private readonly Queue<IRevitRequest> _queue = new();
    private readonly ExternalEvent? _event;
    private bool _insideExecute;
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
        lock (_gate)
            _insideExecute = true;

        try
        {
            while (true)
            {
                IRevitRequest[] batch;
                lock (_gate)
                {
                    if (_queue.Count == 0)
                        break;

                    batch = _queue.ToArray();
                    _queue.Clear();
                }

                foreach (var request in batch)
                    request.Execute(app);
            }
        }
        finally
        {
            var raiseAgain = false;
            lock (_gate)
            {
                _insideExecute = false;
                raiseAgain = _queue.Count > 0;
            }

            if (raiseAgain)
                RaiseExternalEvent();
        }
    }

    string IExternalEventHandler.GetName() => nameof(RevitDispatcher);

    public void Post(Action<UIApplication> action)
    {
        ArgumentNullException.ThrowIfNull(action);

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
        ArgumentNullException.ThrowIfNull(action);

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
        ArgumentNullException.ThrowIfNull(action);

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
        ArgumentNullException.ThrowIfNull(action);

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
        ArgumentNullException.ThrowIfNull(handler);

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
        ArgumentNullException.ThrowIfNull(handler);

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
            _insideExecute = false;
        }

        var exception = new ObjectDisposedException(nameof(RevitDispatcher));
        foreach (var request in pending)
            request.Fail(exception);

        _event?.Dispose();
    }

    /// <summary>
    /// The caller is on the Revit thread in API mode. Nested work runs inline while
    /// <see cref="Execute"/> is on the stack. Otherwise only an empty queue runs inline.
    /// </summary>
    private bool AllowDirectInvocation()
    {
        if (_disposed != 0) return false;
        if (!RevitContext.IsRevitInApiMode) return false;

        lock (_gate)
            return _insideExecute || _queue.Count == 0;
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
            shouldRaise = !_insideExecute;
        }

        if (shouldRaise)
            RaiseExternalEvent();
    }

    private void RaiseExternalEvent()
    {
        try
        {
            var status = _event!.Raise();
            if (status is ExternalEventRequest.Accepted or ExternalEventRequest.Pending)
                return;

            FailPendingRequests(new InvalidOperationException(
                $"ExternalEvent.Raise was not accepted. Request status: {status}."));
        }
        catch (Exception exception)
        {
            FailPendingRequests(exception);
        }
    }

    private void FailPendingRequests(Exception exception)
    {
        IRevitRequest[] pending;

        lock (_gate)
        {
            pending = _queue.ToArray();
            _queue.Clear();
        }

        foreach (var request in pending)
            request.Fail(exception);
    }
}

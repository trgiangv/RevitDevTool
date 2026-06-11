namespace RevitDevTool.ExternalEvent.App.Commands.Adapters;

/// <summary>
/// Baseline: Revit's raw ExternalEvent + IExternalEventHandler with a fixed no-op handler.
/// Raise() triggers the handler on the Revit thread; a TaskCompletionSource bridges to await.
/// </summary>
internal sealed class NativeExternalEventAdapter : IInContextEventAdapter
{
    private const int TimeoutMs = 10_000;
    private readonly Handler _handler = new();
    private readonly Autodesk.Revit.UI.ExternalEvent _externalEvent;

    public NativeExternalEventAdapter()
    {
        _externalEvent = Autodesk.Revit.UI.ExternalEvent.Create(_handler);
    }

    public string Name => "Native ExternalEvent";
    public string DispatchModel => "Raw Revit ExternalEvent + IExternalEventHandler (fixed no-op)";
    public bool SupportsDirectInvocation => false;

    public async Task RaiseAndWaitAsync(CancellationToken token = default)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _handler.SetCompletionSource(tcs);

        var status = _externalEvent.Raise();
        if (status != ExternalEventRequest.Accepted)
            throw new InvalidOperationException($"ExternalEvent.Raise() returned {status}");

        using var reg = token.CanBeCanceled
            ? token.Register(() => tcs.TrySetCanceled(token))
            : default(CancellationTokenRegistration?);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(TimeoutMs, token));
        if (winner != tcs.Task)
            throw new TimeoutException($"NativeExternalEvent: handler not invoked within {TimeoutMs}ms");
        await tcs.Task;
    }

    public void Dispose() => _externalEvent.Dispose();

    private sealed class Handler : IExternalEventHandler
    {
        private TaskCompletionSource<bool>? _tcs;

        public void SetCompletionSource(TaskCompletionSource<bool> tcs)
        {
            Interlocked.Exchange(ref _tcs, tcs);
        }

        public void Execute(UIApplication app)
        {
            Interlocked.Exchange(ref _tcs, null)?.TrySetResult(true);
        }

        public string GetName() => "BenchmarkNoOpHandler";
    }
}

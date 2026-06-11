using Nice3point.Revit.Toolkit.External;
namespace RevitDevTool.ExternalEvent.App.Commands.Adapters;

/// <summary>
/// Tests RevitToolkit as designed: create the AsyncExternalEvent once with a fixed handler,
/// reuse it via RaiseAsync(). Measures event raise/dispatch overhead only.
/// </summary>
internal sealed class RevitToolkitAdapter : IInContextEventAdapter
{
    private const int TimeoutMs = 10_000;
    private readonly AsyncExternalEvent _voidEvent = new(_ => { }, ExternalEventOptions.AllowDirectInvocation);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string Name => "RevitToolkit";
    public string DispatchModel => "AsyncExternalEvent fixed-handler reuse (intended usage)";
    public bool SupportsDirectInvocation => true;

    public async Task RaiseAndWaitAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var task = _voidEvent.RaiseAsync();
            var winner = await Task.WhenAny(task, Task.Delay(TimeoutMs, token));
            if (winner != task)
                throw new TimeoutException($"RevitToolkit: event hung for {TimeoutMs}ms");
            await task;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}

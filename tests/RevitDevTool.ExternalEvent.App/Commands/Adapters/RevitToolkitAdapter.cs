using Nice3point.Revit.Toolkit.External;
namespace RevitDevTool.ExternalEvent.App.Commands.Adapters;

internal sealed class RevitToolkitAdapter : IDispatchAdapter
{
    private const int TimeoutMs = 10_000;

    public string Name => "RevitToolkit";
    public bool SupportsCancellation => false;

    public async Task<T> RunAsync<T>(Func<UIApplication, T> func, CancellationToken token = default)
    {
        var evt = new AsyncRequestExternalEvent<T>(func, ExternalEventOptions.AllowDirectInvocation);
        var task = evt.RaiseAsync();
        return await WaitWithTimeout(task);
    }

    public async Task RunAsync(Action<UIApplication> action, CancellationToken token = default)
    {
        var evt = new AsyncExternalEvent(action, ExternalEventOptions.AllowDirectInvocation);
        var task = evt.RaiseAsync();
        await WaitWithTimeout(task);
    }

    private static async Task<T> WaitWithTimeout<T>(Task<T> task)
    {
        var winner = await Task.WhenAny(task, Task.Delay(TimeoutMs));
        if (winner != task)
            throw new TimeoutException($"RevitToolkit: Raise() likely returned non-Accepted (hung {TimeoutMs}ms)");
        return await task;
    }

    private static async Task WaitWithTimeout(Task task)
    {
        var winner = await Task.WhenAny(task, Task.Delay(TimeoutMs));
        if (winner != task)
            throw new TimeoutException($"RevitToolkit: Raise() likely returned non-Accepted (hung {TimeoutMs}ms)");
        await task;
    }
}

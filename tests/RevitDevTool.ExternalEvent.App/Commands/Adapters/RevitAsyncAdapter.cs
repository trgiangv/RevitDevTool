using Revit.Async;
namespace RevitDevTool.ExternalEvent.App.Commands.Adapters;

internal sealed class RevitAsyncAdapter : IDispatchAdapter
{
    private const int TimeoutMs = 10_000;

    public string Name => "Revit.Async";
    public bool SupportsCancellation => false;

    public async Task<T> RunAsync<T>(Func<UIApplication, T> func, CancellationToken token = default)
    {
        var task = RevitTask.RunAsync(func);
        var winner = await Task.WhenAny(task, Task.Delay(TimeoutMs));
        if (winner != task)
            throw new TimeoutException($"Revit.Async: request hung for {TimeoutMs}ms");
        return await task;
    }

    public async Task RunAsync(Action<UIApplication> action, CancellationToken token = default)
    {
        var task = RevitTask.RunAsync(action);
        var winner = await Task.WhenAny(task, Task.Delay(TimeoutMs));
        if (winner != task)
            throw new TimeoutException($"Revit.Async: request hung for {TimeoutMs}ms");
        await task;
    }
}

using ricaun.Revit.UI.Tasks;
namespace RevitDevTool.ExternalEvent.App.Commands.Adapters;

internal sealed class RicaunTaskAdapter(IRevitTask revitTask) : IDispatchAdapter
{
    private const int TimeoutMs = 10_000;

    public string Name => "ricaun.Revit.UI.Tasks";
    public bool SupportsCancellation => true;
    public bool SupportsDirectInvocation => false;
    public string DispatchModel => "Idling/event-creation service";

    public async Task<T> RunAsync<T>(Func<UIApplication, T> func, CancellationToken token = default)
    {
        var task = revitTask.Run(app => (object)func(app)!, token);
        var winner = await Task.WhenAny(task, Task.Delay(TimeoutMs, token));
        if (winner != task)
            throw new TimeoutException($"ricaun: request hung for {TimeoutMs}ms");
        return (T)await task;
    }

    public async Task RunAsync(Action<UIApplication> action, CancellationToken token = default)
    {
        var task = revitTask.Run(app => { action(app); return (object)true; }, token);
        var winner = await Task.WhenAny(task, Task.Delay(TimeoutMs, token));
        if (winner != task)
            throw new TimeoutException($"ricaun: request hung for {TimeoutMs}ms");
        await task;
    }
}

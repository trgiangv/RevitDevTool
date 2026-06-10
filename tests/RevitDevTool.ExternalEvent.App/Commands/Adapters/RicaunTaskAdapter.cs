using ricaun.Revit.UI.Tasks;
namespace RevitDevTool.ExternalEvent.App.Commands.Adapters;

internal sealed class RicaunTaskAdapter : IDispatchAdapter
{
    private readonly IRevitTask _revitTask;

    public RicaunTaskAdapter(IRevitTask revitTask)
    {
        _revitTask = revitTask;
    }

    public string Name => "ricaun.Revit.UI.Tasks";
    public bool SupportsCancellation => true;
    public bool SupportsDirectInvocation => false;
    public string DispatchModel => "Idling/event-creation service";

    public async Task<T> RunAsync<T>(Func<UIApplication, T> func, CancellationToken token = default)
    {
        var result = await _revitTask.Run(app => (object)func(app)!, token);
        return (T)result;
    }

    public async Task RunAsync(Action<UIApplication> action, CancellationToken token = default)
    {
        await _revitTask.Run(app => { action(app); return (object)true; }, token);
    }
}

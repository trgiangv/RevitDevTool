using RevitDevTool.Core;
namespace RevitDevTool.ExternalEvent.App.Commands.Adapters;

internal sealed class RevitDevToolAdapter : IDispatchAdapter
{
    public string Name => "RevitDevTool.Core";
    public bool SupportsCancellation => true;
    public bool SupportsDirectInvocation => true;
    public string DispatchModel => "Central FIFO dispatcher with batch drain";

    public Task<T> RunAsync<T>(Func<UIApplication, T> func, CancellationToken token = default)
    {
        return RevitContextExecutor.RaiseAsync(func, token);
    }

    public Task RunAsync(Action<UIApplication> action, CancellationToken token = default)
    {
        return RevitContextExecutor.RaiseAsync(action, token);
    }
}

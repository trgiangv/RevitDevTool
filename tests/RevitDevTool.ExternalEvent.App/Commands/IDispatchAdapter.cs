namespace RevitDevTool.ExternalEvent.App.Commands;

internal interface IDispatchAdapter
{
    string Name { get; }
    bool SupportsCancellation { get; }
    bool SupportsDirectInvocation { get; }

    /// <summary>
    /// Short label describing how this adapter dispatches work.
    /// </summary>
    string DispatchModel { get; }

    Task<T> RunAsync<T>(Func<UIApplication, T> func, CancellationToken token = default);
    Task RunAsync(Action<UIApplication> action, CancellationToken token = default);
}

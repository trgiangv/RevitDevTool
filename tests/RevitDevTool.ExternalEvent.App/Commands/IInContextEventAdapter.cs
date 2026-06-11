namespace RevitDevTool.ExternalEvent.App.Commands;

/// <summary>
/// Adapter for in-context event dispatch (Suite 2).
/// The handler is baked at construction time — no per-call delegate.
/// Measures raise/dispatch overhead from normal UI context only.
/// </summary>
internal interface IInContextEventAdapter : IDisposable
{
    string Name { get; }
    string DispatchModel { get; }
    bool SupportsDirectInvocation { get; }

    Task RaiseAndWaitAsync(CancellationToken token = default);
}

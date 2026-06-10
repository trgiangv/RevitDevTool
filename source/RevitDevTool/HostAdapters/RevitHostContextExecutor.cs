using DevTools.Execution.Interfaces;
using RevitDevTool.Core;

namespace RevitDevTool.HostAdapters;

public sealed class RevitHostContextExecutor : IHostContextExecutor
{
    public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default)
        => RevitContextExecutor.RaiseAsync(handler, token);

    public Task ExecuteAsync(Action action, CancellationToken token = default)
        => RevitContextExecutor.RaiseAsync(action, token);
}

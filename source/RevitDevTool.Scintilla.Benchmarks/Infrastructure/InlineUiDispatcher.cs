using RevitDevTool.Scintilla.Services;

namespace RevitDevTool.Scintilla.Benchmarks.Infrastructure;

internal sealed class InlineUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => true;

    public void Invoke(Action action) => action();

    public void BeginInvoke(Action action) => action();
}

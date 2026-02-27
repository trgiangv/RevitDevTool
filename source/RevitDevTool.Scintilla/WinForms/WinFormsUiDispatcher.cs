using RevitDevTool.Scintilla.Core;

namespace RevitDevTool.Scintilla.WinForms;

public sealed class WinFormsUiDispatcher(Control control) : IUiDispatcher
{
    private readonly Control _control = control;

    public bool CheckAccess() => !_control.InvokeRequired;

    public void Invoke(Action action)
    {
        if (_control.IsDisposed)
            return;

        _control.Invoke(action);
    }
}

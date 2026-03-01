namespace RevitDevTool.Scintilla.Services;

public sealed class UiDispatcher(System.Windows.Forms.Control control) : IUiDispatcher
{
    public bool CheckAccess() => !control.InvokeRequired;

    public void Invoke(Action action)
    {
        if (control.IsDisposed)
            return;

        control.Invoke(action);
    }

    public void BeginInvoke(Action action)
    {
        if (control.IsDisposed)
            return;

        control.BeginInvoke(action);
    }
}

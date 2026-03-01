namespace RevitDevTool.Scintilla.Services;

public interface IUiDispatcher
{
    bool CheckAccess();
    void Invoke(Action action);
    void BeginInvoke(Action action);
}

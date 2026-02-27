namespace RevitDevTool.Scintilla.Core;

public interface IUiDispatcher
{
    bool CheckAccess();
    void Invoke(Action action);
}

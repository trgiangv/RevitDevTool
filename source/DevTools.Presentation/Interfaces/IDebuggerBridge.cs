namespace DevTools.Presentation.Interfaces;

public interface IDebuggerBridge
{
    int DebugPort { get; }
    bool IsConnected();
}

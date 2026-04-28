namespace DevTools.Views.Interfaces;

public interface IDebuggerBridge
{
    int DebugPort { get; }
    bool IsConnected();
}

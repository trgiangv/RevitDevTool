namespace DevTools.Presentation.Interfaces;

public interface IDebuggerBridge
{
    int PythonDebugPort { get; }
    int IronPythonDebugPort { get; }
    bool IsPythonConnected();
    bool IsIronPythonConnected();
    bool IsConnected();
}

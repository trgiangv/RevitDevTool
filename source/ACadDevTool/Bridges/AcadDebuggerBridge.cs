using DevTools.Execution.Providers.IronPython;
using DevTools.Execution.Providers.Python;
using DevTools.Presentation.Interfaces;

namespace AcadDevTool.Bridges;

public sealed class AcadDebuggerBridge(IronPythonDebugger ironPythonDebugger) : IDebuggerBridge
{
    public int PythonDebugPort => PythonDebugger.DebugPort;
    public int IronPythonDebugPort => ironPythonDebugger.DebugPort;

    public bool IsPythonConnected() => PythonDebugger.IsConnected();
    public bool IsIronPythonConnected() => ironPythonDebugger.IsAttached;

    public bool IsConnected() => IsPythonConnected() || IsIronPythonConnected();
}

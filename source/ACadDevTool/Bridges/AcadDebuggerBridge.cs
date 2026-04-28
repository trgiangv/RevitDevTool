using DevTools.Execution.Providers.Python;
using DevTools.Views.Interfaces;

namespace AcadDevTool.Bridges;

public sealed class AcadDebuggerBridge : IDebuggerBridge
{
    public int DebugPort => PythonDebugger.DebugPort;
    public bool IsConnected() => PythonDebugger.IsConnected();
}

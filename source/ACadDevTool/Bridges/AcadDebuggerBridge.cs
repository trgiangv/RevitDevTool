using DevTools.Execution.Providers.Python;
using DevTools.Presentation.Interfaces;

namespace AcadDevTool.Bridges;

public sealed class AcadDebuggerBridge : IDebuggerBridge
{
    public int DebugPort => PythonDebugger.DebugPort;
    public bool IsConnected() => PythonDebugger.IsConnected();
}

using DevTools.Execution.Providers.Python;
using DevTools.Views.Interfaces;

namespace RevitDevTool.Bridges;

public sealed class RevitDebuggerBridge : IDebuggerBridge
{
    public int DebugPort => PythonDebugger.DebugPort;
    public bool IsConnected() => PythonDebugger.IsConnected();
}

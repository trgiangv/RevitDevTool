using DevTools.Execution.Interfaces;
using Python.Runtime;
using RevitDevTool.Core;

namespace RevitDevTool.HostAdapters;

public sealed class RevitPythonBridge : IHostPythonBridge
{
    public string ProgramName => "RevitDevTool";

    public void SetupBuiltins(dynamic builtins, PyModule globalScope)
    {
        builtins.__revit__ = RevitContext.UiApplication.ToPython();
    }
}

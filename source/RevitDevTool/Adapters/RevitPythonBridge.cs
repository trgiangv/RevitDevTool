using DevTools.Execution.Interfaces;
using Python.Runtime;
using RevitDevTool.Core;
namespace RevitDevTool.Adapters;

public sealed class RevitPythonBridge : IPythonBridge
{
    public string ProgramName => "RevitDevTool";

    public void SetupBuiltins(dynamic builtins, PyModule globalScope)
    {
        builtins.__revit__ = RevitContext.UiApplication.ToPython();
    }
}

using DevTools.Execution.Interfaces;
using Python.Runtime;

namespace AcadDevTool.HostAdapters;

public sealed class AcadPythonBridge : IHostPythonBridge
{
    public string ProgramName => "AcadDevTool";

    public void SetupBuiltins(dynamic builtins, PyModule globalScope)
    {
        // AutoCAD Application is a static class — accessible directly via:
        // from Autodesk.AutoCAD.ApplicationServices.Core import Application
        // No builtin injection needed (unlike Revit's __revit__ = UIApplication instance).
    }
}

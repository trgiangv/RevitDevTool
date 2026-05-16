using Autodesk.AutoCAD.ApplicationServices;
using DevTools.Execution.Interfaces;
using Microsoft.Scripting.Hosting;

namespace AcadDevTool.HostAdapters;

/// <summary>
/// Minimal AutoCAD assembly exposure for IronPython; further refs stay in script (clr) / embedded setup.
/// </summary>
public sealed class AcadIronPythonBridge : IIronPythonBridge
{
    public void ConfigureEngine(ScriptEngine engine)
    {
        engine.Runtime.LoadAssembly(typeof(Document).Assembly);
    }
}

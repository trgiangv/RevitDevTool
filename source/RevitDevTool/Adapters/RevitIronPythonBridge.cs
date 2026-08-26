using DevTools.Execution.Interfaces;
using Microsoft.Scripting.Hosting;
using RevitDevTool.Core;
namespace RevitDevTool.Adapters;

/// <summary>
/// Injects Revit into IronPython builtins and loads Revit API assemblies into the script engine.
/// </summary>
public sealed class RevitIronPythonBridge : IIronPythonBridge
{
    public void ConfigureEngine(ScriptEngine engine)
    {
        var builtin = IronPython.Hosting.Python.GetBuiltinModule(engine);
        builtin.SetVariable("__revit__", RevitContext.UiApplication);
        engine.Runtime.LoadAssembly(typeof(Document).Assembly);
        engine.Runtime.LoadAssembly(typeof(UIApplication).Assembly);
        engine.Runtime.LoadAssembly(typeof(RevitIronPythonBridge).Assembly);
    }
}

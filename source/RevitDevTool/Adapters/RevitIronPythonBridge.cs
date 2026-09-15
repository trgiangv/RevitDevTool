using DevTools.Execution.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Scripting.Hosting;
using RevitDevTool.Core;
using RevitDevTool.Execution.PyRevit;
using ZLogger;

namespace RevitDevTool.Adapters;

/// <summary>
/// Injects Revit into IronPython builtins and loads Revit API assemblies into the
/// embedded engine (Revit without pyRevit). pyRevit Scripts use ScriptExecutor.
/// </summary>
public sealed class RevitIronPythonBridge(ILogger<RevitIronPythonBridge> logger) : IIronPythonBridge
{
    public object? TryGetHostEngine()
    {
        if (!PyRevitLibraryPaths.IsLoaded)
            return null;

        logger.ZLogInformation(
            $"IronPython debug uses pyRevit ScriptExecutor engine. Root={PyRevitLibraryPaths.InstallRoot}");
        return PyRevitReflectionCache.Instance.EnsureIronPythonEngine(logger);
    }

    public void ConfigureEngine(ScriptEngine engine)
    {
        var builtin = IronPython.Hosting.Python.GetBuiltinModule(engine);
        builtin.SetVariable("__revit__", RevitContext.UiApplication);
        engine.Runtime.LoadAssembly(typeof(Document).Assembly);
        engine.Runtime.LoadAssembly(typeof(UIApplication).Assembly);
        engine.Runtime.LoadAssembly(typeof(RevitIronPythonBridge).Assembly);
    }
}

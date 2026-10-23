using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using DevTools.Execution.Interfaces;
using Microsoft.Scripting.Hosting;

namespace AcadDevTool.Adapters;

/// <summary>
/// Loads the same AutoCAD managed assemblies CPython gets in SetupAcad.py
/// (accoremgd / acdbmgd / acmgd). Extra product DLLs stay in script via clr.
/// </summary>
public sealed class AcadIronPythonBridge : IIronPythonBridge
{
    public object? TryGetHostEngine() => null;

    public void ConfigureEngine(ScriptEngine engine)
    {
        engine.Runtime.LoadAssembly(typeof(CommandMethodAttribute).Assembly); // AcCoreMgd
        engine.Runtime.LoadAssembly(typeof(Database).Assembly); // AcDbMgd
        engine.Runtime.LoadAssembly(typeof(PaletteSet).Assembly); // AcMgd
    }
}

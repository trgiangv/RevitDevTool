using Microsoft.Scripting.Hosting;

namespace DevTools.Execution.Interfaces;

/// <summary>
/// Host-specific IronPython setup (builtins, CLR assemblies). Register in the host add-in.
/// </summary>
public interface IIronPythonBridge
{
    void ConfigureEngine(ScriptEngine engine);

    /// <summary>
    /// Host-owned DLR engine (pyRevit ScriptExecutor), or <see langword="null"/>
    /// to create embedded 3.4.2. Must run on the CAD start thread.
    /// </summary>
    object? TryGetHostEngine();
}

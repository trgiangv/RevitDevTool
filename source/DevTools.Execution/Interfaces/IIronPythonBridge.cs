using Microsoft.Scripting.Hosting;

namespace DevTools.Execution.Interfaces;

/// <summary>
/// Host-specific IronPython setup (builtins, CLR assemblies). Register in the host add-in.
/// </summary>
public interface IIronPythonBridge
{
    void ConfigureEngine(ScriptEngine engine);
}

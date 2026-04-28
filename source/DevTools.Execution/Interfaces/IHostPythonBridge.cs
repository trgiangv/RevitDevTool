using Python.Runtime;

namespace DevTools.Execution.Interfaces;

/// <summary>
/// Host-specific Python environment setup (e.g., injecting host application objects into builtins).
/// </summary>
public interface IHostPythonBridge
{
    void SetupBuiltins(dynamic builtins, PyModule globalScope);
    string ProgramName { get; }
}

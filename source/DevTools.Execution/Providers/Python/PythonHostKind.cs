namespace DevTools.Execution.Providers.Python;

/// <summary>
/// Selects host-specific embedded Python setup (CLR references). Shared scripts live under DevTools.Execution.Resources.scripts.
/// </summary>
public enum PythonHostKind
{
    Revit,
    AutoCad,
    Civil3D,
    Plant3D
}

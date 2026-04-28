using DevTools.Execution.Configs;

namespace AcadDevTool.Settings;

/// <summary>
/// AutoCAD-specific execution config. Inherits all properties from ExecutionConfig.
/// Uses a distinct type name so FileConfig persists to AcadExecutionConfig.json
/// instead of colliding with Revit's ExecutionConfig.json.
/// </summary>
public sealed class AcadExecutionConfig : ExecutionConfig;

using RevitDevTool.Bridge.Enums.Revit;

namespace RevitDevTool.Bridge;

/// <summary>
/// Default values applied to all files unless overridden per-file.
/// Inherits the same nullable schema as <see cref="FileEntry"/> for consistency.
/// Non-null values here serve as the fallback when a file entry leaves a field null.
/// </summary>
public sealed class JobDefaults : JobOptions
{
    public JobDefaults()
    {
        Headless = true;
        DetachFromCentral = CentralMode.DetachAndPreserveWorksets;
        Workset = WorksetMode.OpenAllWorksets;
        AllowOpeningLocalByWrongUser = true;
        IgnoreExtensibleStorageSchemaConflict = true;
        CloseDocument = true;
        CloseHost = false;
    }
}

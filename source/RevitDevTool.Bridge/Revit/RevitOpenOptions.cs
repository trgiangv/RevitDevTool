using MessagePack;
using RevitDevTool.Bridge.Enums.Revit;

namespace RevitDevTool.Bridge.Revit;

/// <summary>
/// Revit-specific open options. Worksets and central model handling
/// are unique to Revit and do not exist in other host applications.
/// </summary>
[MessagePackObject]
public sealed partial class RevitOpenOptions : OpenOptions
{
    [Key(2)] public CentralMode DetachFromCentral { get; set; } = CentralMode.DetachAndPreserveWorksets;
    [Key(3)] public WorksetMode Workset { get; set; } = WorksetMode.OpenAllWorksets;
    [Key(4)] public bool AllowOpeningLocalByWrongUser { get; set; } = true;
    [Key(5)] public bool IgnoreExtensibleStorageSchemaConflict { get; set; } = true;
    [Key(6)] public List<int> OpenWorksets { get; set; } = new();
    [Key(7)] public List<int> CloseWorksets { get; set; } = new();
}

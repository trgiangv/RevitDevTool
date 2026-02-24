using RevitDevTool.Bridge.Enums.Revit;

namespace RevitDevTool.Bridge;

/// <summary>
/// Shared nullable options for both per-file entries and defaults.
/// All properties are nullable so that per-file entries can selectively
/// override only the fields they care about; null means "use the default".
/// </summary>
public class JobOptions
{
    public string? HostVersion { get; set; }
    public string? Script { get; set; }
    public bool? Headless { get; set; }
    public bool? Audit { get; set; }
    public CentralMode? DetachFromCentral { get; set; }
    public WorksetMode? Workset { get; set; }
    public bool? AllowOpeningLocalByWrongUser { get; set; }
    public bool? IgnoreExtensibleStorageSchemaConflict { get; set; }
    public List<int>? OpenWorksets { get; set; }
    public List<int>? CloseWorksets { get; set; }
    public bool? CloseDocument { get; set; }
    public bool? CloseHost { get; set; }
}

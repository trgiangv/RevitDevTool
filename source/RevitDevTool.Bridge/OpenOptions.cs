using MessagePack;
using RevitDevTool.Bridge.Revit;

namespace RevitDevTool.Bridge;

/// <summary>
/// Base open-file options shared by all host applications.
/// Host-specific options (worksets, central mode, etc.) live in derived types.
/// </summary>
[Union(0, typeof(RevitOpenOptions))]
[MessagePackObject]
public abstract partial class OpenOptions
{
    [Key(0)] public bool Headless { get; set; } = true;
    [Key(1)] public bool Audit { get; set; }
}

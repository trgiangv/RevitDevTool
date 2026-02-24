using MessagePack;

namespace RevitDevTool.Bridge;

/// <summary>
/// A fully resolved job ready to be sent over the pipe.
/// All defaults have been merged, version validated.
/// </summary>
[MessagePackObject]
public sealed partial class ResolvedJob
{
    [Key(0)] public string FilePath { get; set; } = "";
    [Key(1)] public string HostVersion { get; set; } = "";
    [Key(2)] public string Script { get; set; } = "";
    [Key(3)] public OpenOptions Open { get; set; } = new Revit.RevitOpenOptions();
    [Key(4)] public LifecyclePolicy Lifecycle { get; set; } = new();
}

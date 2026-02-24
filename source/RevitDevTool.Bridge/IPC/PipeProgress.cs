using MessagePack;

namespace RevitDevTool.Bridge.IPC;

[MessagePackObject]
public sealed partial class PipeProgress
{
    [Key(0)] public string JobId { get; set; } = "";
    [Key(1)] public string Message { get; set; } = "";
    [Key(2)] public int Current { get; set; }
    [Key(3)] public int Total { get; set; }
}

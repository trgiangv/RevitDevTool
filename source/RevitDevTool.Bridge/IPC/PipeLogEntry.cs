using MessagePack;

namespace RevitDevTool.Bridge.IPC;

[MessagePackObject]
public sealed class PipeLogEntry
{
    [Key(0)] public string TimestampUtc { get; set; } = string.Empty;
    [Key(1)] public string Level { get; set; } = "Information";
    [Key(2)] public string Message { get; set; } = string.Empty;
    [Key(3)] public string Source { get; set; } = string.Empty;
    [Key(4)] public string? Exception { get; set; }
}

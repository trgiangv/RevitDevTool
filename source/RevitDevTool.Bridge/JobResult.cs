using MessagePack;

namespace RevitDevTool.Bridge;

[MessagePackObject]
public sealed partial class JobResult
{
    [Key(0)] public bool Success { get; set; }
    [Key(1)] public string? Error { get; set; }
    [Key(2)] public string? StackTrace { get; set; }
    [Key(3)] public long DurationMs { get; set; }
    [Key(4)] public Dictionary<string, string> OutputData { get; set; } = new();

    public static JobResult Ok(long durationMs) => new()
    {
        Success = true,
        DurationMs = durationMs
    };

    public static JobResult Fail(Exception ex, long durationMs) => new()
    {
        Success = false,
        Error = ex.Message,
        StackTrace = ex.StackTrace,
        DurationMs = durationMs
    };
}

namespace RevitDevTool.Logger.Transport;

/// <summary>
/// Transport-neutral log payload used across process boundaries.
/// </summary>
public sealed class LogEventData
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Level { get; set; } = "Information";
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Exception { get; set; }
}

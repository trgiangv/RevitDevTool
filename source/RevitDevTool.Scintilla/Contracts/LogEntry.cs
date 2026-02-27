using System.Collections.ObjectModel;

namespace RevitDevTool.Scintilla.Contracts;

public sealed class LogEntry
{
    public static readonly IReadOnlyDictionary<string, object?> EmptyProperties =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public LogSeverity Level { get; init; } = LogSeverity.Information;
    public string Message { get; init; } = string.Empty;
    public string? ExceptionText { get; init; }
    public string? Source { get; init; }
    public IReadOnlyDictionary<string, object?> Properties { get; init; } = EmptyProperties;
}

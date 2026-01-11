using Microsoft.Extensions.Logging;

namespace RevitDevTool.RichTextBox.Colored;

public sealed class ZLoggerLogEntry
{
    public ZLoggerLogEntry(
        LogLevel level,
        DateTimeOffset timestamp,
        string category,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        Level = level;
        Timestamp = timestamp;
        Category = category;
        Message = message;
        Exception = exception;
        Properties = properties ?? new Dictionary<string, object?>();
    }

    public LogLevel Level { get; }

    public DateTimeOffset Timestamp { get; }

    public string Category { get; }

    public string Message { get; }

    public Exception? Exception { get; }

    public IReadOnlyDictionary<string, object?> Properties { get; }
}

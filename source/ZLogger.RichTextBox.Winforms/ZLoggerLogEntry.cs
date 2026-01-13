using Microsoft.Extensions.Logging;

namespace ZLogger.RichTextBox.Winforms;

public sealed class ZLoggerLogEntry(
    LogLevel level,
    DateTimeOffset timestamp,
    string category,
    string message,
    Exception? exception = null,
    IReadOnlyDictionary<string, object?>? properties = null)
{
    public LogLevel Level { get; } = level;

    public DateTimeOffset Timestamp { get; } = timestamp;

    public string Category { get; } = category;

    public string Message { get; } = message;

    public Exception? Exception { get; } = exception;

    public IReadOnlyDictionary<string, object?> Properties { get; } = properties ?? new Dictionary<string, object?>();
}

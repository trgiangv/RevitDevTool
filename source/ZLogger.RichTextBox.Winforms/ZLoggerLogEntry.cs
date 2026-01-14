using Microsoft.Extensions.Logging;

namespace ZLogger.RichTextBox.Winforms;

/// <summary>
/// Represents a log entry for display in RichTextBox.
/// </summary>
public readonly struct ZLoggerLogEntry
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyProperties = 
        new Dictionary<string, object?>();
        
    public LogLevel Level { get; }
    public DateTimeOffset Timestamp { get; }
    public string Category { get; }
    public string Message { get; }
    public Exception? Exception { get; }
    public IReadOnlyDictionary<string, object?>? Properties { get; }
    
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
        Properties = properties;
    }
        
    public IReadOnlyDictionary<string, object?> PropertiesOrEmpty => 
        Properties ?? EmptyProperties;
}

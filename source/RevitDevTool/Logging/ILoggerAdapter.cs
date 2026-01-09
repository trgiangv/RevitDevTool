namespace RevitDevTool.Logging;

/// <summary>
/// Abstraction for logging operations, allowing different logging frameworks to be used interchangeably.
/// </summary>
public interface ILoggerAdapter : IDisposable
{
    void Verbose(string messageTemplate, params object?[] propertyValues);
    void Debug(string messageTemplate, params object?[] propertyValues);
    void Information(string messageTemplate, params object?[] propertyValues);
    void Warning(string messageTemplate, params object?[] propertyValues);
    void Error(string messageTemplate, params object?[] propertyValues);
    void Error(Exception exception, string messageTemplate, params object?[] propertyValues);
    void Fatal(string messageTemplate, params object?[] propertyValues);
    void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues);
    
    void Write(LogLevel level, string messageTemplate, params object?[] propertyValues);
    void Write(LogLevel level, Exception? exception, string messageTemplate, params object[] propertyValues);
    
    ILoggerAdapter ForContext(string propertyName, object? value);
    ILoggerAdapter ForContext<T>() where T : class;
}

public enum LogLevel
{
    Verbose = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Fatal = 5
}


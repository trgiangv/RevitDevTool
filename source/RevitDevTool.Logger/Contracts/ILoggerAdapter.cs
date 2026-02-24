using Microsoft.Extensions.Logging;

namespace RevitDevTool.Logger.Contracts;

/// <summary>
/// Host-agnostic logger adapter contract.
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

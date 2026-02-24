using Microsoft.Extensions.Logging;
using RevitDevTool.Logger.Contracts;
using ILogger = Serilog.ILogger;

namespace RevitDevTool.Logger.Serilog;

/// <summary>
/// Host-agnostic Serilog adapter implementation.
/// </summary>
public class SerilogAdapter : ILoggerAdapter
{
    protected readonly ILogger Logger;
    private bool _disposed;

    public SerilogAdapter(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Verbose(string messageTemplate, params object?[] propertyValues) => Logger.Verbose(messageTemplate, propertyValues);
    public void Debug(string messageTemplate, params object?[] propertyValues) => Logger.Debug(messageTemplate, propertyValues);
    public void Information(string messageTemplate, params object?[] propertyValues) => Logger.Information(messageTemplate, propertyValues);
    public void Warning(string messageTemplate, params object?[] propertyValues) => Logger.Warning(messageTemplate, propertyValues);
    public void Error(string messageTemplate, params object?[] propertyValues) => Logger.Error(messageTemplate, propertyValues);
    public void Error(Exception exception, string messageTemplate, params object?[] propertyValues) => Logger.Error(exception, messageTemplate, propertyValues);
    public void Fatal(string messageTemplate, params object?[] propertyValues) => Logger.Fatal(messageTemplate, propertyValues);
    public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues) => Logger.Fatal(exception, messageTemplate, propertyValues);
    public void Write(LogLevel level, string messageTemplate, params object?[] propertyValues) => Logger.Write(level.ToSerilog(), messageTemplate, propertyValues);
    public void Write(LogLevel level, Exception? exception, string messageTemplate, params object[] propertyValues) => Logger.Write(level.ToSerilog(), exception, messageTemplate, propertyValues);
    public ILoggerAdapter ForContext(string propertyName, object? value) => new SerilogAdapter(Logger.ForContext(propertyName, value));
    public ILoggerAdapter ForContext<T>() where T : class => new SerilogAdapter(Logger.ForContext(typeof(T)));

    public void Dispose()
    {
        if (_disposed) return;
        (Logger as IDisposable)?.Dispose();
        _disposed = true;
    }
}

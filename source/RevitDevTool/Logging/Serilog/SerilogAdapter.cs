using Serilog.Events;
using ILogger = Serilog.ILogger;
// ReSharper disable TemplateIsNotCompileTimeConstantProblem
// ReSharper disable ContextualLoggerProblem

namespace RevitDevTool.Logging.Serilog;

/// <summary>
/// Serilog implementation of ILoggerAdapter.
/// </summary>
internal sealed class SerilogAdapter(ILogger logger) : ILoggerAdapter
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool _disposed;

    public void Verbose(string messageTemplate, params object?[] propertyValues)
        => _logger.Verbose(messageTemplate, propertyValues);

    public void Debug(string messageTemplate, params object?[] propertyValues)
        => _logger.Debug(messageTemplate, propertyValues);

    public void Information(string messageTemplate, params object?[] propertyValues)
        => _logger.Information(messageTemplate, propertyValues);

    public void Warning(string messageTemplate, params object?[] propertyValues)
        => _logger.Warning(messageTemplate, propertyValues);

    public void Error(string messageTemplate, params object?[] propertyValues)
        => _logger.Error(messageTemplate, propertyValues);

    public void Error(Exception exception, string messageTemplate, params object?[] propertyValues)
        => _logger.Error(exception, messageTemplate, propertyValues);

    public void Fatal(string messageTemplate, params object?[] propertyValues)
        => _logger.Fatal(messageTemplate, propertyValues);

    public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues)
        => _logger.Fatal(exception, messageTemplate, propertyValues);

    public void Write(LogLevel level, string messageTemplate, params object?[] propertyValues)
        => _logger.Write(ToSerilogLevel(level), messageTemplate, propertyValues);

    public void Write(LogLevel level, Exception? exception, string messageTemplate, params object[] propertyValues)
        => _logger.Write(ToSerilogLevel(level), exception, messageTemplate, propertyValues);

    public ILoggerAdapter ForContext(string propertyName, object? value)
        => new SerilogAdapter(_logger.ForContext(propertyName, value));

    public ILoggerAdapter ForContext<T>() where T : class
        => new SerilogAdapter(_logger.ForContext<T>());

    private static LogEventLevel ToSerilogLevel(LogLevel level) => level switch
    {
        LogLevel.Verbose => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        LogLevel.Fatal => LogEventLevel.Fatal,
        _ => LogEventLevel.Debug
    };

    public void Dispose()
    {
        if (_disposed) return;
        (_logger as IDisposable)?.Dispose();
        _disposed = true;
    }
}


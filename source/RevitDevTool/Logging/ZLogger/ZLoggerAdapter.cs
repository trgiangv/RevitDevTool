using Microsoft.Extensions.Logging;
// ReSharper disable TemplateIsNotCompileTimeConstantProblem
#pragma warning disable CA2254

namespace RevitDevTool.Logging.ZLogger;

/// <summary>
/// ZLogger implementation of ILoggerAdapter.
/// Wraps Microsoft.Extensions.Logging.ILogger with ZLogger extensions.
/// </summary>
[UsedImplicitly]
internal sealed class ZLoggerAdapter(ILogger logger) : ILoggerAdapter
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool _disposed;

    public void Verbose(string messageTemplate, params object?[] propertyValues)
        => _logger.Log(LogLevel.Trace, messageTemplate, propertyValues);

    public void Debug(string messageTemplate, params object?[] propertyValues)
        => _logger.Log(LogLevel.Debug, messageTemplate, propertyValues);

    public void Information(string messageTemplate, params object?[] propertyValues)
        => _logger.Log(LogLevel.Information, messageTemplate, propertyValues);

    public void Warning(string messageTemplate, params object?[] propertyValues)
        => _logger.Log(LogLevel.Warning, messageTemplate, propertyValues);

    public void Error(string messageTemplate, params object?[] propertyValues)
        => _logger.Log(LogLevel.Error, messageTemplate, propertyValues);

    public void Error(Exception exception, string messageTemplate, params object?[] propertyValues)
        => _logger.Log(LogLevel.Error, exception, messageTemplate, propertyValues);

    public void Fatal(string messageTemplate, params object?[] propertyValues)
        => _logger.Log(LogLevel.Critical, messageTemplate, propertyValues);

    public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues)
        => _logger.Log(LogLevel.Critical, exception, messageTemplate, propertyValues);

    public void Write(LogLevel level, string messageTemplate, params object?[] propertyValues)
        => _logger.Log(level, messageTemplate, propertyValues);

    public void Write(LogLevel level, Exception? exception, string messageTemplate, params object[] propertyValues)
        => _logger.Log(level, exception, messageTemplate, propertyValues);

    public ILoggerAdapter ForContext(string propertyName, object? value)
    {
        // ZLogger doesn't have built-in context like Serilog
        // Return same adapter since ZLogger handles this differently
        return this;
    }

    public ILoggerAdapter ForContext<T>() where T : class
    {
        // ZLogger doesn't have built-in typed context like Serilog
        return this;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

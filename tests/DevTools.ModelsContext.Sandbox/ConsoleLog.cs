using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.ModelsContext.Sandbox;

internal sealed class ConsoleLog(LogLevel minimum) : ILogger
{
    public static ILogger Current { get; set; } = NullLogger.Instance;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= minimum && logLevel != LogLevel.None;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var error = exception is null ? string.Empty : " | " + exception.GetType().Name + ": " + exception.Message;
        Console.WriteLine($"  [{logLevel}] {formatter(state, exception)}{error}");
    }
}

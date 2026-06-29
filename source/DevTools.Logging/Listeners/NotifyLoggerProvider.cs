using Microsoft.Extensions.Logging;

namespace DevTools.Logging.Listeners;

/// <summary>
/// An <see cref="ILoggerProvider"/> that raises <see cref="NotifyListener.TraceReceived"/>
/// for every log entry passing through the <see cref="ILogger"/> pipeline, bridging
/// ILogger output to the UI notification channel.
/// </summary>
[ProviderAlias("Notify")]
public sealed class NotifyLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => NotifyLogger.Instance;

    public void Dispose() { }

    private sealed class NotifyLogger : ILogger
    {
        public static readonly NotifyLogger Instance = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.None) return;
            NotifyListener.RaiseTraceReceived();
        }
    }
}

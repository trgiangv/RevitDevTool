using Microsoft.Extensions.Logging;

namespace DevTools.Mcp.Transport;

/// <summary>
///     Logger factory for one pipe session. Once the session is closing, the SDK reports every request still open
///     (<c>subscriptions/listen</c>) as a failed handler with <see cref="OperationCanceledException" />; those entries
///     are dropped. Every other entry, and any cancellation before the session closes, reaches the host log.
/// </summary>
internal sealed class SessionLoggerFactory(ILoggerFactory inner, CancellationToken closing) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName) => new SessionLogger(inner.CreateLogger(categoryName), closing);

    public void AddProvider(ILoggerProvider provider) => inner.AddProvider(provider);

    public void Dispose()
    {
    }

    private sealed class SessionLogger(ILogger inner, CancellationToken closing) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (exception is OperationCanceledException && closing.IsCancellationRequested)
                return;

            inner.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}

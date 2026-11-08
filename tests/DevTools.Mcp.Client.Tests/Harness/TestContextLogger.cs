using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DevTools.Mcp.Client.Tests.Harness;

internal sealed class TestContextLoggerFactory(TestContext context) : ILoggerFactory
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public ILogger CreateLogger(string categoryName) => new TestContextLogger(context, categoryName, _clock);

    public ILogger<T> CreateLogger<T>() => new Logger<T>(this);

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }

    private sealed class TestContextLogger(TestContext context, string category, Stopwatch clock) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var line = $"[{clock.ElapsedMilliseconds,6} ms] {logLevel} {category}: {formatter(state, exception)}";
            if (exception is not null)
                line += $" | {exception.GetType().Name}: {exception.Message}";
            context.WriteLine(line);
        }
    }
}

using DevTools.ModelsContext.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class LoggingTests
{
    [TestMethod]
    public void SwallowedErrorLogsTrace()
    {
        var logger = new CollectingLogger(LogLevel.Trace);
        var error = new InvalidOperationException("boom");

        logger.Swallowed(error, "View ids", "model.rvt");

        var entry = logger.Entries.Single();
        Assert.AreEqual(LogLevel.Trace, entry.Level);
        Assert.AreSame(error, entry.Exception);
        StringAssert.Contains(entry.Message, "View ids model.rvt");
    }

    [TestMethod]
    public void SliceFailuresLogOnce()
    {
        var logger = new CollectingLogger(LogLevel.Trace);
        var tally = FailureTally.Start(logger)!;
        var first = new InvalidOperationException("first");

        tally.Add(first);
        tally.Add(new InvalidOperationException("second"));
        tally.Add(new InvalidOperationException("third"));
        tally.Flush("Count", "model.rvt", 10);

        var entry = logger.Entries.Single();
        Assert.AreEqual(LogLevel.Trace, entry.Level);
        Assert.AreSame(first, entry.Exception);
        StringAssert.Contains(entry.Message, "3 of 10 failed");
    }

    [TestMethod]
    public void TraceOffLogsNothing()
    {
        var logger = new CollectingLogger(LogLevel.Debug);

        using (TraceTimer.Start(logger, "Panel build"))
        {
        }

        logger.Swallowed(new InvalidOperationException("boom"), "View ids", "model.rvt");

        Assert.IsNull(FailureTally.Start(logger));
        Assert.IsFalse(TraceTimer.Start(logger).IsEnabled);
        Assert.IsEmpty(logger.Entries);
    }

    [TestMethod]
    public void TimerLogsElapsedAtTrace()
    {
        var logger = new CollectingLogger(LogLevel.Trace);

        using (TraceTimer.Start(logger, "Panel build"))
        {
        }

        var entry = logger.Entries.Single();
        Assert.AreEqual(LogLevel.Trace, entry.Level);
        StringAssert.StartsWith(entry.Message, "Panel build in ");
    }

    [TestMethod]
    public async Task VisibleFailureLogsError()
    {
        var logger = new CollectingLogger(LogLevel.Debug);
        using var fixture = await PanelFixture.OpenAsync(logger: logger);
        fixture.Filter.AddCommand.Execute(null);
        fixture.Model.StoreError = "Settings file is locked.";

        fixture.Filter.SaveCommand.Execute(null);

        var entry = logger.Entries.Single();
        Assert.AreEqual(LogLevel.Error, entry.Level);
        Assert.IsInstanceOfType<InvalidOperationException>(entry.Exception);
        StringAssert.Contains(entry.Message, "Settings file is locked.");
    }

    [TestMethod]
    public async Task PanelLoadTracesTimings()
    {
        var logger = new CollectingLogger(LogLevel.Trace);

        using var fixture = await PanelFixture.OpenAsync(logger: logger);

        var messages = logger.Entries.Select(entry => entry.Message).ToList();
        Assert.IsTrue(messages.Any(message => message.StartsWith("Snapshot ", StringComparison.Ordinal)));
        Assert.IsTrue(messages.Any(message => message.StartsWith("Context load ", StringComparison.Ordinal)));
        Assert.IsTrue(logger.Entries.All(entry => entry.Level == LogLevel.Trace));
    }

    private sealed class CollectingLogger(LogLevel minimum) : ILogger
    {
        public List<Entry> Entries { get; } = [];

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
            if (IsEnabled(logLevel))
            {
                Entries.Add(new Entry(logLevel, formatter(state, exception), exception));
            }
        }
    }

    private sealed record Entry(LogLevel Level, string Message, Exception? Exception);
}

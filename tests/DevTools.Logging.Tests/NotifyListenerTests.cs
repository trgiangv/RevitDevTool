using System.Diagnostics;
using DevTools.Logging.Listeners;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging.Tests;

[TestClass]
public sealed class NotifyListenerTests
{
    [TestMethod]
    public void NotifyListener_and_provider_raise_trace_received()
    {
        var count = 0;
        void Handler() => count++;
        NotifyListener.TraceReceived += Handler;

        try
        {
            var listener = new NotifyListener();
            listener.Write("a");
            listener.WriteLine("b");
            listener.TraceEvent(null, "src", TraceEventType.Information, 1);
            listener.TraceEvent(null, "src", TraceEventType.Warning, 2, "msg");
            listener.TraceEvent(null, "src", TraceEventType.Error, 3, "fmt {0}", "arg");
            listener.TraceData(null, "src", TraceEventType.Verbose, 4, "data");
            listener.TraceData(null, "src", TraceEventType.Verbose, 5, "a", "b");
            listener.Write(string.Empty);
            listener.WriteLine(null);

            NotifyListener.RaiseTraceReceived();

            var provider = new NotifyLoggerProvider();
            var logger = provider.CreateLogger("cat");
            Assert.IsTrue(logger.IsEnabled(LogLevel.Debug));
            logger.Log(LogLevel.Information, default, "state", null, static (s, _) => s?.ToString() ?? "");
            logger.Log(LogLevel.None, default, "ignored", null, static (s, _) => s?.ToString() ?? "");

            Assert.AreEqual(9, count);
        }
        finally
        {
            NotifyListener.TraceReceived -= Handler;
        }
    }

    [TestMethod]
    public void NotifyLoggerProvider_dispose_is_noop()
    {
        using var provider = new NotifyLoggerProvider();
        provider.Dispose();
    }
}

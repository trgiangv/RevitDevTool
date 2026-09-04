using System.Diagnostics;
using DevTools.Logging.Listeners;

namespace DevTools.Logging.Tests;

public sealed class ConsoleRedirectorTests
{
    [Fact]
    public void ConsoleRedirector_routes_console_to_trace_and_restores_on_dispose()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var seen = new List<string>();
        var traceListener = new CaptureTraceListener(seen);
        Trace.Listeners.Add(traceListener);

        try
        {
            using (new ConsoleRedirector())
            {
                Console.Write('x');
                Console.Write("hello");
                Console.Write(['a', 'b', 'c'], 1, 2);
                Console.WriteLine();
                Console.WriteLine("line");
                Console.Error.WriteLine("err");
                Console.Out.Flush();
            }

            Assert.Same(originalOut, Console.Out);
            Assert.Same(originalError, Console.Error);
            Assert.Contains("x", string.Concat(seen), StringComparison.Ordinal);
            Assert.Contains("hello", string.Concat(seen), StringComparison.Ordinal);
            Assert.Contains("line", string.Concat(seen), StringComparison.Ordinal);
            Assert.Contains("err", string.Concat(seen), StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(traceListener);
        }
    }

    private sealed class CaptureTraceListener(List<string> seen) : TraceListener
    {
        public override void Write(string? message)
        {
            if (!string.IsNullOrEmpty(message))
                seen.Add(message);
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrEmpty(message))
                seen.Add(message);
        }
    }
}

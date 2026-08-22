using System.Diagnostics;
using System.Text;

namespace DevTools.Testing.Abstractions.Runtime;

/// <summary>
/// Silent per-case buffer of <see cref="Trace"/> / <see cref="Debug"/> for
/// <c>CaseResult.Output</c> (Test Explorer). Framework-captured Console is
/// forwarded to process <see cref="Trace"/> at case finish via
/// <see cref="WriteThrough"/> so the host pane sees it without duplicating IDE stdout.
/// </summary>
public sealed class TestingRunTraceScope : IDisposable
{
    private readonly Listener _listener = new();
    private bool _disposed;

    public TestingRunTraceScope() => Trace.Listeners.Insert(0, _listener);

    public string? CompleteCase() => _listener.Take();

    /// <summary>
    /// Forwards framework-captured Console text to process <see cref="Trace"/>
    /// (host pane) without copying it into the IDE buffer.
    /// </summary>
    public void WriteThrough(string? text)
    {
        if (text is null || text.Length == 0)
            return;

        var trimmed = text.TrimEnd('\r', '\n');
        if (trimmed.Length == 0)
            return;

        _listener.SuspendCapture();
        try
        {
            Trace.Write(trimmed);
        }
        finally
        {
            _listener.ResumeCapture();
        }
    }

    public static string? Merge(string? frameworkOutput, string? traceOutput)
    {
        var hasFramework = !string.IsNullOrWhiteSpace(frameworkOutput);
        var hasTrace = !string.IsNullOrWhiteSpace(traceOutput);
        if (hasFramework && hasTrace)
            return frameworkOutput!.TrimEnd() + Environment.NewLine + traceOutput!.TrimEnd();
        if (hasFramework)
            return frameworkOutput;
        return hasTrace ? traceOutput : null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Trace.Listeners.Remove(_listener);
        _listener.Dispose();
    }

    private sealed class Listener : TraceListener
    {
        private readonly object _sync = new();
        private readonly StringBuilder _buffer = new();
        private int _suspendCount;

        public override bool IsThreadSafe => true;

        public void SuspendCapture()
        {
            lock (_sync)
                _suspendCount++;
        }

        public void ResumeCapture()
        {
            lock (_sync)
            {
                if (_suspendCount > 0)
                    _suspendCount--;
            }
        }

        public override void Write(string? message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            lock (_sync)
            {
                if (_suspendCount > 0)
                    return;

                _buffer.Append(message);
            }
        }

        public override void Write(string? message, string? category)
        {
            if (string.IsNullOrWhiteSpace(category))
                Write(message);
            else
                Write($"[{category}] {message}");
        }

        public override void WriteLine(string? message) =>
            Write(string.IsNullOrEmpty(message) ? Environment.NewLine : message + Environment.NewLine);

        public override void WriteLine(string? message, string? category)
        {
            if (string.IsNullOrWhiteSpace(category))
                WriteLine(message);
            else
                WriteLine($"[{category}] {message}");
        }

        public string? Take()
        {
            lock (_sync)
            {
                var text = _buffer.ToString();
                _buffer.Clear();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
        }
    }
}

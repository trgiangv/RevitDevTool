using System.Diagnostics;
using System.Text;

namespace DevTools.NUnit.Runtime;

/// <summary>
/// Silent per-case buffer of <see cref="Trace"/> / <see cref="Debug"/> for
/// <c>CaseResult.Output</c> (Test Explorer). Console captured by NUnit is
/// forwarded to process <see cref="Trace"/> at case finish via
/// <see cref="WriteThrough"/> so the host pane sees it without duplicating IDE stdout.
/// </summary>
internal sealed class NUnitRunTraceScope : IDisposable
{
    private readonly Listener _listener = new();
    private bool _disposed;

    public NUnitRunTraceScope() => Trace.Listeners.Insert(0, _listener);

    public string? CompleteCase() => _listener.Take();

    /// <summary>
    /// Forwards NUnit-captured Console/<c>TestContext</c> text to process
    /// <see cref="Trace"/> (host pane) without copying it into the IDE buffer.
    /// </summary>
    public void WriteThrough(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        text = text.TrimEnd('\r', '\n');
        if (text.Length == 0)
            return;

        _listener.SuspendCapture();
        try
        {
            Trace.Write(text);
        }
        finally
        {
            _listener.ResumeCapture();
        }
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

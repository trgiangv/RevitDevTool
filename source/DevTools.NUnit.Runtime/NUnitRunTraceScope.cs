using System.Diagnostics;
using System.Text;

namespace DevTools.NUnit.Runtime;

/// <summary>
/// Captures <see cref="Trace"/> / <see cref="Debug"/> in the same process as the
/// test assembly (net48 AppDomain; net8+ collectible ALC).
/// </summary>
internal sealed class NUnitRunTraceScope : IDisposable
{
    private readonly Listener _listener = new();
    private bool _disposed;

    public NUnitRunTraceScope() => Trace.Listeners.Insert(0, _listener);

    public string? CompleteCase() => _listener.Take();

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

        public override bool IsThreadSafe => true;

        public override void Write(string? message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            lock (_sync)
                _buffer.Append(message);
        }

        public override void WriteLine(string? message) =>
            Write(string.IsNullOrEmpty(message) ? Environment.NewLine : message + Environment.NewLine);

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

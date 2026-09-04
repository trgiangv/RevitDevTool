using System.Diagnostics;
using System.Text;
// ReSharper disable RedundantSuppressNullableWarningExpression

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
    private readonly TraceListener[] _snapshot;
    private bool _disposed;

    public TestingRunTraceScope()
    {
        _snapshot = SnapshotListeners();
        EnsureRegistered();
    }

    public string? CompleteCase()
    {
        EnsureRegistered();
        return _listener.Take();
    }

    /// <summary>
    /// Forwards framework-captured Console text to process <see cref="Trace"/>
    /// (host pane) without copying it into the IDE buffer.
    /// </summary>
    public void WriteThrough(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var trimmed = text!.TrimEnd('\r', '\n');
        if (trimmed.Length == 0)
            return;

        EnsureRegistered();
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
        RestoreListeners();
        _listener.Dispose();
    }

    private void EnsureRegistered()
    {
        if (Trace.Listeners.Contains(_listener))
        {
            var index = Trace.Listeners.IndexOf(_listener);
            if (index > 0)
            {
                Trace.Listeners.RemoveAt(index);
                Trace.Listeners.Insert(0, _listener);
            }

            return;
        }

        Trace.Listeners.Insert(0, _listener);
    }

    private static TraceListener[] SnapshotListeners()
    {
        var listeners = new TraceListener[Trace.Listeners.Count];
        Trace.Listeners.CopyTo(listeners, 0);
        return listeners;
    }

    private void RestoreListeners()
    {
        Trace.Listeners.Remove(_listener);

        var desired = new List<TraceListener>(_snapshot.Length);
        foreach (var listener in _snapshot)
        {
            if (listener != _listener && !desired.Contains(listener))
                desired.Add(listener);
        }

        Trace.Listeners.Clear();
        foreach (var listener in desired)
            Trace.Listeners.Add(listener);
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

        public override void Write(string? message, string? category) =>
            Write(string.IsNullOrWhiteSpace(category) ? message : $"[{category}] {message}");

        public override void WriteLine(string? message) =>
            Write(string.IsNullOrEmpty(message) ? Environment.NewLine : message + Environment.NewLine);

        public override void WriteLine(string? message, string? category) =>
            WriteLine(string.IsNullOrWhiteSpace(category) ? message : $"[{category}] {message}");

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

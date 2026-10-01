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
public sealed class TestRunTraceScope : IDisposable
{
    private readonly Listener _listener;
    private readonly TraceListener[] _snapshot;
    private bool _disposed;

    public TestRunTraceScope(Func<string?>? caseKey = null)
    {
        _listener = new Listener(caseKey);
        _snapshot = SnapshotListeners();
        EnsureRegistered();
    }

    public string? CompleteCase(string? caseKey = null)
    {
        EnsureRegistered();
        return _listener.Take(caseKey);
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

    private sealed class Listener(Func<string?>? caseKey) : TraceListener
    {
        private readonly Lock _sync = new();
        private readonly Dictionary<string, StringBuilder> _buckets = new(StringComparer.Ordinal);
        private int _suspendCount;

        public override bool IsThreadSafe => true;

        private string CurrentKey()
        {
            var key = caseKey?.Invoke();
            return string.IsNullOrEmpty(key) ? string.Empty : key!;
        }

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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_sync)
                    _buckets.Clear();
            }

            base.Dispose(disposing);
        }

        public override void Write(string? message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            lock (_sync)
            {
                if (_suspendCount > 0)
                    return;

                var key = CurrentKey();
                if (!_buckets.TryGetValue(key, out var buffer))
                {
                    buffer = new StringBuilder();
                    _buckets[key] = buffer;
                }

                buffer.Append(message);
            }
        }

        public override void Write(string? message, string? category) =>
            Write(string.IsNullOrWhiteSpace(category) ? message : $"[{category}] {message}");

        public override void WriteLine(string? message) =>
            Write(string.IsNullOrEmpty(message) ? Environment.NewLine : message + Environment.NewLine);

        public override void WriteLine(string? message, string? category) =>
            WriteLine(string.IsNullOrWhiteSpace(category) ? message : $"[{category}] {message}");

        public string? Take(string? key)
        {
            key ??= string.Empty;
            lock (_sync)
            {
                if (!_buckets.TryGetValue(key, out var buffer))
                    return null;

                _buckets.Remove(key);
                var text = buffer.ToString();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
        }
    }
}

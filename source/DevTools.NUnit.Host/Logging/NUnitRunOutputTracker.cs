using System.Text;

namespace DevTools.NUnit.Host.Logging;

/// <summary>
/// Buffers Trace/Debug output per active NUnit test-case id during a run session.
/// </summary>
public sealed class NUnitRunOutputTracker
{
    private readonly object _sync = new();
    private readonly Dictionary<string, StringBuilder> _buffersByTestId = new(StringComparer.Ordinal);
    private string? _activeTestId;

    public void BeginTest(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        lock (_sync)
        {
            _activeTestId = id;
            if (!_buffersByTestId.ContainsKey(id))
                _buffersByTestId[id] = new StringBuilder();
        }
    }

    public void Append(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        lock (_sync)
        {
            if (_activeTestId is null || !_buffersByTestId.TryGetValue(_activeTestId, out var buffer))
                return;

            buffer.Append(text);
        }
    }

    public string? Complete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        lock (_sync)
        {
            if (string.Equals(_activeTestId, id, StringComparison.Ordinal))
                _activeTestId = null;

            if (!_buffersByTestId.Remove(id, out var buffer))
                return null;

            var text = buffer.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }
}

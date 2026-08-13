using System.Text;

namespace DevTools.NUnit.Host.Logging;

/// <summary>
/// Buffers Trace/Debug text for the in-flight NUnit case. Tests run sequentially
/// on the Autodesk thread, so one current buffer is enough.
/// </summary>
public sealed class NUnitRunOutputTracker
{
    private readonly object _sync = new();
    private StringBuilder? _current;

    public void BeginTest()
    {
        lock (_sync)
            _current = new StringBuilder();
    }

    public void Append(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        lock (_sync)
        {
            _current ??= new StringBuilder();
            _current.Append(text);
        }
    }

    public string? Complete()
    {
        lock (_sync)
        {
            var buffer = _current;
            _current = null;
            if (buffer is null)
                return null;

            var text = buffer.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }
}

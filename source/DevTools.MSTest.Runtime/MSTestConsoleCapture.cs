using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevTools.MSTest.Runtime;

/// <summary>
/// Buffers <see cref="Console.Out"/> and <see cref="Console.Error"/> for one
/// in-host run. <c>mstest:output:captureTrace</c> stays false, so MSTest does
/// not take those writers. Each write is stored under the display name of
/// <see cref="TestContext.Current"/> while the body runs. The terminal node
/// reads that bucket later. Draining one shared buffer when the node message
/// arrives mixes data rows: the next row can write before the previous message
/// is consumed. The original writers are restored when the run ends.
/// </summary>
internal sealed class MSTestConsoleCapture : IDisposable
{
    private TextWriter? _originalOut;
    private TextWriter? _originalError;
    private readonly BufferWriter _stdout = new();
    private readonly BufferWriter _stderr = new();
    private bool _started;
    private bool _disposed;

    public void Start()
    {
        if (_started || _disposed)
            return;

        _originalOut = Console.Out;
        _originalError = Console.Error;
        Console.SetOut(_stdout);
        Console.SetError(_stderr);
        _started = true;
    }

    public string? CompleteCase(string? displayName)
    {
        var stdout = _stdout.Take(displayName);
        var stderr = _stderr.Take(displayName);
        if (string.IsNullOrWhiteSpace(stdout))
            return string.IsNullOrWhiteSpace(stderr) ? null : stderr;
        return string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + Environment.NewLine + stderr;
    }

    public void Dispose()
    {
        if (_disposed || !_started)
            return;

        _disposed = true;
        var stdout = _stdout.TakeAll();
        var stderr = _stderr.TakeAll();
        var originalOut = _originalOut ?? Console.Out;
        var originalError = _originalError ?? Console.Error;
        Console.SetOut(originalOut);
        Console.SetError(originalError);
        if (!string.IsNullOrEmpty(stdout))
            originalOut.Write(stdout);
        if (!string.IsNullOrEmpty(stderr))
            originalError.Write(stderr);
    }

    private sealed class BufferWriter : TextWriter
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<string, StringBuilder> _buckets = new(StringComparer.Ordinal);

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => Append(value.ToString());

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            Append(value!);
        }

        public override void Write(char[] buffer, int index, int count) =>
            Append(new string(buffer, index, count));

        public string? Take(string? displayName)
        {
            var key = string.IsNullOrEmpty(displayName) ? string.Empty : displayName!;
            lock (_gate)
                return Remove(key);
        }

        public string? TakeAll()
        {
            lock (_gate)
            {
                if (_buckets.Count == 0)
                    return null;

                var text = string.Concat(_buckets.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value));
                _buckets.Clear();
                return text.Length == 0 ? null : text;
            }
        }

        private static string CaseKey()
        {
#pragma warning disable MSTESTEXP // TestContext.Current is the row that is executing.
            var displayName = TestContext.Current?.TestDisplayName;
#pragma warning restore MSTESTEXP
            return string.IsNullOrEmpty(displayName) ? string.Empty : displayName!;
        }

        private void Append(string value)
        {
            var key = CaseKey();
            lock (_gate)
            {
                if (!_buckets.TryGetValue(key, out var buffer))
                {
                    buffer = new StringBuilder();
                    _buckets[key] = buffer;
                }

                buffer.Append(value);
            }
        }

        private string? Remove(string key)
        {
            if (!_buckets.TryGetValue(key, out var buffer))
                return null;

            _buckets.Remove(key);
            return buffer.Length == 0 ? null : buffer.ToString();
        }
    }
}

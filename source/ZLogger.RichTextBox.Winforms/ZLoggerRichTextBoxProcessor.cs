using System.Buffers;
using System.Text;
using ZLogger.RichTextBox.Winforms.Collections;
using ZLogger.RichTextBox.Winforms.Extensions;
using ZLogger.RichTextBox.Winforms.Rendering;
using ZLogger.RichTextBox.Winforms.Rtf;

namespace ZLogger.RichTextBox.Winforms;

/// <summary>
/// ZLogger log processor that outputs to a RichTextBox control with RTF formatting.
/// Uses immediate formatting to avoid holding ZLogger entry references.
/// </summary>
public sealed class ZLoggerRichTextBoxProcessor : IAsyncLogProcessor, IDisposable
{
    private const double FlushIntervalMs = 1000.0 / 16.0;
    private readonly ConcurrentCircularBuffer<ZLoggerLogEntry> _buffer;
    private readonly AutoResetEvent _signal;
    private readonly ZLoggerRichTextBoxOptions _options;
    private readonly ITokenRenderer _renderer;
    private readonly System.Windows.Forms.RichTextBox _richTextBox;
    private readonly CancellationTokenSource _tokenSource;
    private readonly Task _processingTask;
    
    private bool _disposed;

    public ZLoggerRichTextBoxProcessor(
        System.Windows.Forms.RichTextBox richTextBox,
        ZLoggerRichTextBoxOptions options,
        ITokenRenderer? renderer = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _richTextBox = richTextBox ?? throw new ArgumentNullException(nameof(richTextBox));
        _renderer = renderer ?? new ZLoggerTemplateRenderer(options);
        _tokenSource = new CancellationTokenSource();

        _buffer = new ConcurrentCircularBuffer<ZLoggerLogEntry>(options.MaxLogLines);
        _signal = new AutoResetEvent(false);

        InitializeRichTextBox();
        _processingTask = Task.Run(() => ProcessMessages(_tokenSource.Token));
    }

    private void InitializeRichTextBox()
    {
        if (_richTextBox.InvokeRequired)
        {
            _richTextBox.Invoke(InitializeRichTextBoxInternal);
        }
        else
        {
            InitializeRichTextBoxInternal();
        }
    }

    private void InitializeRichTextBoxInternal()
    {
        _richTextBox.Clear();
        _richTextBox.ReadOnly = true;
        _richTextBox.DetectUrls = false;
        _richTextBox.ForeColor = _options.Theme.DefaultStyle.Foreground;
        _richTextBox.BackColor = _options.Theme.DefaultStyle.Background;
    }

    public void Post(IZLoggerEntry log)
    {
        if (_disposed) return;

        var logInfo = log.LogInfo;
        
        // Format message immediately and release entry
        var message = FormatMessage(log, _options.MaxMessageLength);
        
        var entry = new ZLoggerLogEntry(
            logInfo.LogLevel,
            logInfo.Timestamp.Local,
            logInfo.Category.Name,
            message,
            logInfo.Exception);

        _buffer.Add(entry);
        _signal.Set();
        
        // Return entry to ZLogger pool immediately
        log.Return();
    }
    
    private static string FormatMessage(IZLoggerEntry entry, int maxLength)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        entry.ToString(buffer);
        
#if NET5_0_OR_GREATER
        var written = buffer.WrittenSpan;
        if (maxLength > 0 && written.Length > maxLength)
        {
            var truncateAt = FindUtf8SafeBoundary(written, maxLength - 20);
            return Encoding.UTF8.GetString(written.Slice(0, truncateAt)) + "... [truncated]";
        }
        return Encoding.UTF8.GetString(written);
#else
        var writtenArray = buffer.WrittenMemory.ToArray();
        if (maxLength > 0 && writtenArray.Length > maxLength)
        {
            var truncateAt = FindUtf8SafeBoundaryArray(writtenArray, maxLength - 20);
            return Encoding.UTF8.GetString(writtenArray, 0, truncateAt) + "... [truncated]";
        }
        return Encoding.UTF8.GetString(writtenArray);
#endif
    }
    
#if NET5_0_OR_GREATER
    private static int FindUtf8SafeBoundary(ReadOnlySpan<byte> data, int targetLength)
    {
        if (targetLength >= data.Length) return data.Length;
        var pos = targetLength;
        while (pos > 0 && (data[pos] & 0xC0) == 0x80) pos--;
        return pos;
    }
#else
    private static int FindUtf8SafeBoundaryArray(byte[] data, int targetLength)
    {
        if (targetLength >= data.Length) return data.Length;
        var pos = targetLength;
        while (pos > 0 && (data[pos] & 0xC0) == 0x80) pos--;
        return pos;
    }
#endif

    public void Clear()
    {
        if (_disposed) return;
        _buffer.Clear();
        _signal.Set();
    }

    public void Restore()
    {
        if (_disposed) return;
        _buffer.Restore();
        _signal.Set();
    }

    private void ProcessMessages(CancellationToken token)
    {
        using var builder = new RtfBuilder(_options.Theme);
        var snapshot = new List<ZLoggerLogEntry>(_options.MaxLogLines);
        var flushInterval = TimeSpan.FromMilliseconds(FlushIntervalMs);
        var lastFlush = DateTime.MinValue;

        while (!token.IsCancellationRequested)
        {
            if (!WaitForSignal(token, flushInterval, ref lastFlush))
                break;

            RenderSnapshot(builder, snapshot, token);
            lastFlush = DateTime.UtcNow;
        }
    }

    private bool WaitForSignal(CancellationToken token, TimeSpan flushInterval, ref DateTime lastFlush)
    {
        _signal.WaitOne();
        if (token.IsCancellationRequested) return false;

        var elapsed = DateTime.UtcNow - lastFlush;
        if (elapsed >= flushInterval) return true;

        var remaining = flushInterval - elapsed;
        return !token.WaitHandle.WaitOne(remaining);
    }

    private void RenderSnapshot(RtfBuilder builder, List<ZLoggerLogEntry> snapshot, CancellationToken token)
    {
        _signal.Reset();
        _buffer.TakeSnapshot(snapshot);
        builder.Clear();

        foreach (var entry in snapshot)
        {
            _renderer.Render(entry, builder);
        }

        if (_richTextBox.IsDisposed || _richTextBox.Disposing) return;

        _richTextBox.SetRtf(builder.Rtf, _options.AutoScroll, token);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _tokenSource.Cancel();
        _signal.Set();
        _processingTask.ConfigureAwait(true);
        _signal.Dispose();
        _tokenSource.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}

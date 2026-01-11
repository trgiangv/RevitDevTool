using System.Buffers;
using System.Text;
using RevitDevTool.RichTextBox.Colored.Collections;
using RevitDevTool.RichTextBox.Colored.Extensions;
using RevitDevTool.RichTextBox.Colored.Rendering;
using RevitDevTool.RichTextBox.Colored.Rtf;
using ZLogger;

namespace RevitDevTool.RichTextBox.Colored;

/// <summary>
/// ZLogger log processor that outputs to a RichTextBox control with RTF formatting.
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

        try
        {
            var logInfo = log.LogInfo;
            var buffer = new ArrayBufferWriter<byte>();
            log.ToString(buffer);
#if NET5_0_OR_GREATER
            var message = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
            var message = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif

            var properties = new Dictionary<string, object?>();
            for (var i = 0; i < log.ParameterCount; i++)
            {
                var key = log.GetParameterKeyAsString(i);
                var value = log.GetParameterValue(i);
                properties[key] = value;
            }

            var entry = new ZLoggerLogEntry(
                logInfo.LogLevel,
                logInfo.Timestamp.Local,
                logInfo.Category.Name,
                message,
                logInfo.Exception,
                properties);

            _buffer.Add(entry);
            _signal.Set();
        }
        finally
        {
            log.Return();
        }
    }

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
        var builder = new RtfBuilder(_options.Theme);
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

        WaitForProcessingTask();

        _signal.Dispose();
        _tokenSource.Dispose();
    }

    private async void WaitForProcessingTask()
    {
        try
        {
            // _processingTask.Wait(TimeSpan.FromSeconds(2));
            await _processingTask.ConfigureAwait(false);
        }
        catch
        {
            // Ignore exceptions during disposal
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}

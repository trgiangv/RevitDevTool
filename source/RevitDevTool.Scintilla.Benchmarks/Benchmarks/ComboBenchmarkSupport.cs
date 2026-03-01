using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Control;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Logger;
using Serilog.Core;
using Serilog.Events;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace RevitDevTool.Scintilla.Benchmarks.Benchmarks;

internal static class ComboBenchmarkSupport
{
    private const int WmPrintClient = 0x0318;
    private const int PrfClient = 0x00000004;
    private const int PrfEraseBkgnd = 0x00000008;
    private const int PrfChildren = 0x00000010;

    public static void WaitForScintillaDrain(ILogViewerController controller, int timeoutMs = 2000)
        => WaitForScintillaDrain(controller, expectedRenderedDelta: 0, timeoutMs);

    public static void WaitForScintillaDrain(ILogViewerController controller, int expectedRenderedDelta, int timeoutMs = 2000)
    {
        var startRendered = controller.RenderedMessages;
        var targetRendered = expectedRenderedDelta > 0
            ? startRendered + expectedRenderedDelta
            : startRendered;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var drained = controller.IngestBacklogEstimate <= 0;
            var rendered = expectedRenderedDelta <= 0 || controller.RenderedMessages >= targetRendered;
            if (drained && rendered)
            {
                Thread.SpinWait(128);
                drained = controller.IngestBacklogEstimate <= 0;
                rendered = expectedRenderedDelta <= 0 || controller.RenderedMessages >= targetRendered;
                if (drained && rendered)
                    return;
            }

            Thread.Sleep(0);
        }
    }

    public static void WaitForRichTextStable(RichTextBox richTextBox, int timeoutMs = 3000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lastLength = -1;
        var stableTicks = 0;
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var current = richTextBox.TextLength;
            if (current == lastLength)
            {
                stableTicks++;
                if (stableTicks >= 3)
                    return;
            }
            else
            {
                stableTicks = 0;
                lastLength = current;
            }

            Thread.Sleep(5);
        }
    }

    public static void PrepareOffscreenControl(System.Windows.Forms.Control control, int width = 1200, int height = 800)
    {
        if (control.Width != width || control.Height != height)
            control.Size = new Size(width, height);

        if (!control.IsHandleCreated)
            control.CreateControl();
    }

    public static int ForcePaintAndHash(System.Windows.Forms.Control control)
    {
        if (!control.IsHandleCreated)
            control.CreateControl();

        control.Invalidate(true);
        control.Update();

        var width = Math.Max(1, control.ClientSize.Width);
        var height = Math.Max(1, control.ClientSize.Height);
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        var hdc = graphics.GetHdc();
        try
        {
            var flags = (IntPtr)(PrfClient | PrfEraseBkgnd | PrfChildren);
            SendMessage(control.Handle, WmPrintClient, hdc, flags);
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }

        return ComputeSparseBitmapHash(bitmap);
    }

    public static void WaitForRichTextLineCountAtLeast(RichTextBox richTextBox, int expectedLines, int timeoutMs = 3000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (GetRichTextLogicalLineCount(richTextBox) >= expectedLines)
                return;

            Thread.Sleep(1);
        }
    }

    public static LogLevel MapLevel(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => LogLevel.Information
        };
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    private static int ComputeSparseBitmapHash(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            unchecked
            {
                const int fnvOffset = unchecked((int)2166136261);
                const int fnvPrime = 16777619;
                var hash = fnvOffset;

                var xStep = Math.Max(1, bitmap.Width / 16);
                var yStep = Math.Max(1, bitmap.Height / 16);
                for (var y = 0; y < bitmap.Height; y += yStep)
                {
                    for (var x = 0; x < bitmap.Width; x += xStep)
                    {
                        var pixelOffset = (y * data.Stride) + (x * 4);
                        var pixel = Marshal.ReadInt32(data.Scan0, pixelOffset);
                        hash ^= pixel;
                        hash *= fnvPrime;
                    }
                }

                return hash;
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static int GetRichTextLogicalLineCount(RichTextBox richTextBox)
    {
        var count = richTextBox.Lines.Length;
        if (count <= 0)
            return 0;

        return richTextBox.Lines[count - 1].Length == 0 ? count - 1 : count;
    }

    internal sealed class OffscreenPainter : IDisposable
    {
        private readonly Bitmap _bitmap;
        private readonly Graphics _graphics;

        public OffscreenPainter(int width, int height)
        {
            _bitmap = new Bitmap(Math.Max(1, width), Math.Max(1, height), PixelFormat.Format32bppPArgb);
            _graphics = Graphics.FromImage(_bitmap);
        }

        public int PaintAndHash(System.Windows.Forms.Control control)
        {
            if (!control.IsHandleCreated)
                control.CreateControl();

            control.Invalidate(true);
            control.Update();

            var hdc = _graphics.GetHdc();
            try
            {
                var flags = (IntPtr)(PrfClient | PrfEraseBkgnd | PrfChildren);
                SendMessage(control.Handle, WmPrintClient, hdc, flags);
            }
            finally
            {
                _graphics.ReleaseHdc(hdc);
            }

            return ComputeSparseBitmapHash(_bitmap);
        }

        public void Dispose()
        {
            _graphics.Dispose();
            _bitmap.Dispose();
        }
    }

    internal sealed class ScintillaSerilogSink : ILogEventSink
    {
        private readonly ILogEntrySink _sink;

        public ScintillaSerilogSink(ILogViewerController controller)
        {
            if (controller is not ILogEntrySink sink)
                throw new InvalidOperationException("Controller does not expose sink contract.");

            _sink = sink;
        }

        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage(CultureInfo.InvariantCulture);
            var bytes = Encoding.UTF8.GetBytes(message);
            _sink.TryPost(new LogEntry
            {
                TimestampUtc = logEvent.Timestamp.UtcDateTime,
                Level = MapLevel(logEvent.Level),
                Source = "Serilog",
                Message = new ArraySegment<byte>(bytes, 0, bytes.Length),
                Properties = LogEntry.EmptyProperties
            });
        }
    }

    internal sealed class PlainRichTextBoxSerilogSink : ILogEventSink
    {
        private readonly RichTextBox _richTextBox;
        private readonly object _gate = new();

        public PlainRichTextBoxSerilogSink(RichTextBox richTextBox)
        {
            _richTextBox = richTextBox;
        }

        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage(CultureInfo.InvariantCulture);
            lock (_gate)
            {
                _richTextBox.AppendText(message);
                _richTextBox.AppendText(Environment.NewLine);
            }
        }
    }
}

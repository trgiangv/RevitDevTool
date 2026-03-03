using System.Globalization;
using System.Windows.Forms.Integration;
using RevitDevTool.Logging.Linkify;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using Serilog;
using Serilog.Sinks.RichTextBoxForms;
using Serilog.Sinks.RichTextBoxForms.Themes;
using Serilog.Sinks.RichTextBoxForms.Tokens;
using FontStyle = System.Drawing.FontStyle;
using SerilogTheme = Serilog.Sinks.RichTextBoxForms.Themes.Theme;
using RichTextBoxSink = Serilog.Sinks.RichTextBoxForms.RichTextBoxSink;

namespace RevitDevTool.Logging;

/// <summary>
/// RichTextBox sink implementation for Serilog.
/// Provides themed log output to a WinForms RichTextBox control.
/// </summary>
[UsedImplicitly]
public sealed class RichTextBoxMonitor : ILoggingMonitor
{
    private readonly RichTextBox _richTextBox;
    private readonly WindowsFormsHost _host;
    private RichTextBoxSink? _richTextBoxSink;
    private bool _disposed;

    public RichTextBoxMonitor()
    {
        _richTextBox = new RichTextBox
        {
            Font = new Font("Cascadia Mono", 9f, FontStyle.Regular, GraphicsUnit.Point),
            ReadOnly = true,
            DetectUrls = false,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None
        };

        _host = new WindowsFormsHost { Child = _richTextBox };
    }

    /// <summary>
    /// Clears the log output using the library sink's Clear method.
    /// This properly clears both the internal buffer and the UI.
    /// </summary>
    public void Clear()
    {
        if (_richTextBoxSink != null)
        {
            _richTextBoxSink.Clear();
        }
        else
        {
            if (_richTextBox.InvokeRequired)
                _richTextBox.Invoke(() => _richTextBox.Clear());
            else
                _richTextBox.Clear();
        }
    }

    public object GetHostControl() => _host;

    private const string DefaultOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";
    private const string StackTraceOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{StackTrace}{NewLine}{Exception}";

    /// <summary>
    /// Configures Serilog with a RichTextBox sink and stores the reference
    /// to the library's sink for proper lifecycle management.
    /// The sink automatically subscribes to ThemeManager theme changes.
    /// </summary>
    internal LoggerConfiguration ConfigureSerilog(LoggerConfiguration config, bool isDarkTheme, bool prettyPrintJson, bool includeStackTrace)
    {
        DisposeSink();
        var theme = isDarkTheme ? ThemePresets.EnhancedDark : ThemePresets.EnhancedLight;
        var outputTemplate = includeStackTrace ? StackTraceOutputTemplate : DefaultOutputTemplate;
        var options = new RichTextBoxSinkOptions
        {
            Theme = theme,
            AutoScroll = true,
            MaxLogLines = 1000,
            OutputTemplate = outputTemplate,
            FormatProvider = CultureInfo.InvariantCulture,
            PrettyPrintJson = prettyPrintJson,
            EnableTokenLinks = true,
            OnTokenClicked = OnTokenClicked,
            TokenDetector = RevitTokenDetector.Instance,
            MinimumLogEventLevel = Serilog.Events.LogEventLevel.Verbose
        };

        var result = config.WriteTo.RichTextBox(
            _richTextBox,
            out var sink,
            options,
            onThemeChanged: SubscribeToThemeChanges);

        _richTextBoxSink = sink;
        return result;
    }

    private static Action SubscribeToThemeChanges(Action<SerilogTheme> setTheme)
    {
        EventHandler<EventArgs> handler = (_, _) =>
        {
            var isDark = ThemeManager.Current.ActualApplicationTheme == AppTheme.Dark;
            var newTheme = isDark ? ThemePresets.EnhancedDark : ThemePresets.EnhancedLight;
            setTheme(newTheme);
        };

        ThemeManager.Current.ActualApplicationThemeChanged += handler;
        return () => ThemeManager.Current.ActualApplicationThemeChanged -= handler;
    }

    private static void OnTokenClicked(DetectedToken token)
    {
        DispatcherHelper.RunOnMainThread(() => RevitSearchService.TrySearchAndSelectInActiveDocument(token));
    }

    /// <summary>
    /// Disposes the current library sink. Called during restart to properly
    /// clean up the processing task and buffers.
    /// </summary>
    private void DisposeSink()
    {
        if (_richTextBoxSink == null) return;
        _richTextBoxSink.Dispose();
        _richTextBoxSink = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        DisposeSink();
        _host.Dispose();
        _richTextBox.Dispose();
        _disposed = true;
    }
}

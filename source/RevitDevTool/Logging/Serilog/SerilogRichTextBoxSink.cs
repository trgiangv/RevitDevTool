using RevitDevTool.Logging.Theme;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using Serilog;
using System.Globalization;
using System.Windows.Forms.Integration;
using FontStyle = System.Drawing.FontStyle;
using LibrarySink = Serilog.Sinks.RichTextBoxForms.RichTextBoxSink;

namespace RevitDevTool.Logging.Serilog;

/// <summary>
/// RichTextBox sink implementation for Serilog.
/// Provides themed log output to a WinForms RichTextBox control.
/// </summary>
[UsedImplicitly]
internal sealed class SerilogRichTextBoxSink : ILogOutputSink
{
    private readonly System.Windows.Forms.RichTextBox _richTextBox;
    private readonly WindowsFormsHost _host;
    private LibrarySink? _librarySink;
    private bool _disposed;

    public SerilogRichTextBoxSink()
    {
        _richTextBox = new System.Windows.Forms.RichTextBox
        {
            Font = new Font("Cascadia Mono", 9f, FontStyle.Regular, GraphicsUnit.Point),
            ReadOnly = true,
            DetectUrls = true,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None
        };

        _host = new WindowsFormsHost { Child = _richTextBox };
        _host.Loaded += (_, _) => ApplyTheme(ThemeManager.Current.ActualApplicationTheme == AppTheme.Dark);
    }

    /// <summary>
    /// Clears the log output using the library sink's Clear method.
    /// This properly clears both the internal buffer and the UI.
    /// </summary>
    public void Clear()
    {
        if (_librarySink != null)
        {
            _librarySink.Clear();
        }
        else
        {
            if (_richTextBox.InvokeRequired)
                _richTextBox.Invoke(() => _richTextBox.Clear());
            else
                _richTextBox.Clear();
        }
    }

    public void SetTheme(bool isDarkTheme)
    {
        if (_host.Dispatcher.CheckAccess())
            ApplyTheme(isDarkTheme);
        else
            _host.Dispatcher.Invoke(() => ApplyTheme(isDarkTheme));
    }

    public object GetHostControl() => _host;

    /// <summary>
    /// Configures Serilog with a RichTextBox sink and stores the reference
    /// to the library's sink for proper lifecycle management.
    /// </summary>
    internal LoggerConfiguration ConfigureSerilog(LoggerConfiguration config, bool isDarkTheme)
    {
        DisposeSink();
        var logTheme = isDarkTheme ? LogThemePresets.EnhancedDark : LogThemePresets.EnhancedLight;
        var theme = logTheme.ToSerilogTheme();
        var result = config.WriteTo.RichTextBox(
            _richTextBox,
            out var sink,
            theme: theme,
            autoScroll: true
,
            maxLogLines: 1000,
            formatProvider: CultureInfo.InvariantCulture,
            prettyPrintJson: true);

        _librarySink = sink;
        return result;
    }

    /// <summary>
    /// Disposes the current library sink. Called during restart to properly
    /// clean up the processing task and buffers.
    /// </summary>
    private void DisposeSink()
    {
        if (_librarySink == null) return;
        _librarySink.Dispose();
        _librarySink = null;
    }

    private void ApplyTheme(bool isDarkTheme)
    {
        _richTextBox.BackColor = isDarkTheme
            ? LogThemePresets.DarkBackground
            : LogThemePresets.LightBackground;

        Win32Utils.SetImmersiveDarkMode(_richTextBox.Handle, isDarkTheme);
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

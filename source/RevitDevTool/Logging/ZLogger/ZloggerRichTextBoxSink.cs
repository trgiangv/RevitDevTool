using Microsoft.Extensions.Logging;
using RevitDevTool.Logging.Theme;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using System.Windows.Forms.Integration;
using ZLogger.RichTextBox.Winforms;
using FontStyle = System.Drawing.FontStyle;

namespace RevitDevTool.Logging.ZLogger;

/// <summary>
/// RichTextBox sink implementation for ZLogger.
/// Provides themed log output to a WinForms RichTextBox control.
/// </summary>
[UsedImplicitly]
internal sealed class ZloggerRichTextBoxSink : ILogOutputSink
{
    private readonly RichTextBox _richTextBox;
    private readonly WindowsFormsHost _host;
    private readonly object _syncLock = new();
    private ZLoggerRichTextBoxLoggerProvider? _provider;
    private bool _disposed;
    private bool _themeInitialized;

    public ZloggerRichTextBoxSink()
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
        _host.Loaded += OnHostLoaded;
    }

    private void OnHostLoaded(object sender, EventArgs e)
    {
        lock (_syncLock)
        {
            if (_themeInitialized) return;
            _themeInitialized = true;
            _richTextBox.SetRichTextBoxTheme(ThemeManager.Current.ActualApplicationTheme == AppTheme.Dark);
        }
    }

    /// <summary>
    /// Clears the log output using the provider's processor.
    /// </summary>
    public void Clear()
    {
        lock (_syncLock)
        {
            if (_disposed) return;

            if (_provider != null)
            {
                _provider.Processor.Clear();
            }
            else
            {
                if (_richTextBox.InvokeRequired)
                    _richTextBox.Invoke(() => _richTextBox.Clear());
                else
                    _richTextBox.Clear();
            }
        }
    }

    public void SetTheme(bool isDarkTheme)
    {
        if (_disposed) return;
        if (_host.Dispatcher.CheckAccess())
            _richTextBox.SetRichTextBoxTheme(isDarkTheme);
        else
            _host.Dispatcher.BeginInvoke(new Action(() => _richTextBox.SetRichTextBoxTheme(isDarkTheme)));
    }

    public object GetHostControl() => _host;

    /// <summary>
    /// Configures ZLogger with a RichTextBox provider and stores the reference
    /// for proper lifecycle management.
    /// </summary>
    internal void ConfigureZLogger(ILoggingBuilder builder, bool isDarkTheme)
    {
        lock (_syncLock)
        {
            DisposeProvider();

            var logTheme = isDarkTheme ? LogThemePresets.EnhancedDark : LogThemePresets.EnhancedLight;
            var theme = logTheme.ToZLoggerTheme();

            builder.AddZLoggerRichTextBoxUnmanaged(_richTextBox, out var provider, options =>
            {
                options.Theme = theme;
                options.MaxLogLines = 1000;
                options.AutoScroll = true;
                options.OutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}";
                options.PrettyPrintJson = true;
            });

            _provider = provider;
        }
    }

    /// <summary>
    /// Disposes the current provider. Called during restart to properly
    /// clean up the processing task and buffers.
    /// </summary>
    private void DisposeProvider()
    {
        if (_provider == null) return;
        _provider.Dispose();
        _provider = null;
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_disposed) return;
            _disposed = true;
            _host.Loaded -= OnHostLoaded;
            DisposeProvider();
            _host.Dispose();
            _richTextBox.Dispose();
        }
    }
}

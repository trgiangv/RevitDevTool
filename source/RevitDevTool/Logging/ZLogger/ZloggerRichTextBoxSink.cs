using System.Windows.Forms.Integration;
using Microsoft.Extensions.Logging;
using RevitDevTool.Logging.Theme;
using RevitDevTool.RichTextBox.Colored;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using FontStyle = System.Drawing.FontStyle;

namespace RevitDevTool.Logging.ZLogger;

/// <summary>
/// RichTextBox sink implementation for ZLogger.
/// Provides themed log output to a WinForms RichTextBox control.
/// </summary>
[UsedImplicitly]
internal sealed class ZloggerRichTextBoxSink : ILogOutputSink
{
    private readonly System.Windows.Forms.RichTextBox _richTextBox;
    private readonly WindowsFormsHost _host;
    private ZLoggerRichTextBoxLoggerProvider? _provider;
    private bool _disposed;

    public ZloggerRichTextBoxSink()
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
    /// Clears the log output using the provider's processor.
    /// </summary>
    public void Clear()
    {
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

    public void SetTheme(bool isDarkTheme)
    {
        if (_host.Dispatcher.CheckAccess())
            ApplyTheme(isDarkTheme);
        else
            _host.Dispatcher.Invoke(() => ApplyTheme(isDarkTheme));
    }

    public object GetHostControl() => _host;

    /// <summary>
    /// Configures ZLogger with a RichTextBox provider and stores the reference
    /// for proper lifecycle management.
    /// </summary>
    internal void ConfigureZLogger(ILoggingBuilder builder, bool isDarkTheme)
    {
        DisposeProvider();
        
        var logTheme = isDarkTheme ? LogThemePresets.EnhancedDark : LogThemePresets.EnhancedLight;
        var theme = logTheme.ToZLoggerTheme();
        
        builder.AddZLoggerRichTextBox(_richTextBox, out var provider, options =>
        {
            options.Theme = theme;
            options.MaxLogLines = 1000;
            options.AutoScroll = true;
            options.OutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}";
        });

        _provider = provider;
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
        DisposeProvider();
        _host.Dispose();
        _richTextBox.Dispose();
        _disposed = true;
    }
}

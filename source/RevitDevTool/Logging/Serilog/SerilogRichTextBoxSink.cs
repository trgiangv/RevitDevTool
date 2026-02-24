using RevitDevTool.Theme;
using Serilog;
using Serilog.Sinks.RichTextBoxForms.Tokens;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms.Integration;
using RevitDevTool.Logger.Contracts;
using Serilog.Sinks.RichTextBoxForms.Themes;
using FontStyle = System.Drawing.FontStyle;
using SerilogTheme = Serilog.Sinks.RichTextBoxForms.Themes.Theme;
using LibrarySink = Serilog.Sinks.RichTextBoxForms.RichTextBoxSink;

namespace RevitDevTool.Logging.Serilog;

/// <summary>
/// RichTextBox sink implementation for Serilog.
/// Provides themed log output to a WinForms RichTextBox control.
/// </summary>
[UsedImplicitly]
internal sealed class SerilogRichTextBoxSink : ILogOutputSink
{
    private readonly RichTextBox _richTextBox;
    private readonly WindowsFormsHost _host;
    private readonly Dictionary<string, DetectedToken> _detectedTokens = new(StringComparer.Ordinal);
    private LibrarySink? _librarySink;
    private bool _disposed;

    public SerilogRichTextBoxSink()
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

        var result = config.WriteTo.RichTextBox(
            _richTextBox,
            out var sink,
            theme: theme,
            autoScroll: true,
            maxLogLines: 1000,
            outputTemplate: outputTemplate,
            formatProvider: CultureInfo.InvariantCulture,
            prettyPrintJson: prettyPrintJson,
            enableTokenLinks: true,
            enableAutoTokenDetection: true,
            onTokensDetected: OnTokensDetected,
            onTokenClicked: OnTokenClicked,
            onThemeChanged: SubscribeToThemeChanges);

        _librarySink = sink;
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

    private void OnTokensDetected(DetectedTokenBatch batch)
    {
        if (batch.Tokens.Count == 0)
        {
            return;
        }

        if (_host.Dispatcher.CheckAccess())
        {
            CacheSearchableTokens(batch);
        }
        else
        {
            _host.Dispatcher.BeginInvoke(() => CacheSearchableTokens(batch));
        }
    }

    private void CacheSearchableTokens(DetectedTokenBatch batch)
    {
        lock (_detectedTokens)
        {
            foreach (var token in batch.Tokens)
            {
                if (RevitTokenSearchService.SearchActiveDocument(token).Count > 0)
                {
                    _detectedTokens[$"{token.Kind}:{token.NormalizedValue}"] = token;
                }
            }
        }
    }

    private void OnTokenClicked(DetectedToken token)
    {
        // Callbacks can be invoked from the sink processing thread; marshal to UI/Revit thread.
        if (_host.Dispatcher.CheckAccess())
        {
            _ = RevitTokenSearchService.TrySearchAndSelectInActiveDocument(token);
        }
        else
        {
            _host.Dispatcher.BeginInvoke(() => _ = RevitTokenSearchService.TrySearchAndSelectInActiveDocument(token));
        }
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

    public void Dispose()
    {
        if (_disposed) return;
        DisposeSink();
        _host.Dispose();
        _richTextBox.Dispose();
        _disposed = true;
    }
}

using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using RevitDevTool.Models;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Windows.Forms.Integration;
using Wpf.Ui.Appearance;

namespace RevitDevTool.ViewModel;

internal partial class TraceLogViewModel : ObservableObject, IDisposable
{
    public WindowsFormsHost LogTextBox { get; }

    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly ConsoleRedirector _consoleRedirector;
    
    private SerilogTraceListener? _traceListener;
    private Logger? _logger;
    
    private readonly RichTextBox _winFormsTextBox;
    private readonly FrameworkElement _resourceOwner;
    private bool _forceMonoOnLight;
    private System.Drawing.Color _currentForeColor = System.Drawing.Color.Black;
    

    [ObservableProperty] private bool _isStarted = true;
    [ObservableProperty] private LogEventLevel _logLevel = LogEventLevel.Debug;

    partial void OnLogLevelChanged(LogEventLevel value)
    {
        _levelSwitch.MinimumLevel = value;
    }

    partial void OnIsStartedChanged(bool value)
    {
        TraceStatus(value);
    }
    
    private void TraceStatus(bool isStarted)
    {
        if (isStarted)
        {
            Initialized();
            Trace.Listeners.Add(_traceListener!);
            Trace.Listeners.Add(TraceGeometry.TraceListener);
            VisualizationController.Start();
        }
        else
        {
            Trace.Listeners.Remove(_traceListener);
            Trace.Listeners.Remove(TraceGeometry.TraceListener);
            VisualizationController.Stop();
            CloseAndFlush();
        }
    }

    private void Initialized()
    {
        _logger ??= new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .WriteTo.RichTextBox(_winFormsTextBox)
            .CreateLogger();
        _traceListener ??= new SerilogTraceListener(_logger);
    }

    private void CloseAndFlush()
    {
        _winFormsTextBox.Clear();
        _logger?.Dispose();
        _traceListener?.Dispose();
    }
    

    public TraceLogViewModel(FrameworkElement resourceOwner)
    {
        _resourceOwner = resourceOwner;
        _winFormsTextBox = new RichTextBox
        {
            Font = new Font("Cascadia Mono", 9f, System.Drawing.FontStyle.Regular, GraphicsUnit.Point),
            ReadOnly = true,
            DetectUrls = true,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None
        };

        LogTextBox = new WindowsFormsHost
        {
            Child = _winFormsTextBox
        };
        
        _winFormsTextBox.TextChanged += OnWinFormsTextChanged;
        
        PresentationTraceSources.ResourceDictionarySource.Switch.Level = SourceLevels.Critical;
        _levelSwitch = new LoggingLevelSwitch(_logLevel);
        _consoleRedirector = new ConsoleRedirector();
        
        ApplicationThemeManager.Changed += OnThemeChanged;
        TraceStatus(IsStarted);
    }

    private void OnThemeChanged(ApplicationTheme theme, System.Windows.Media.Color accent)
    {
        LogTextBox.Dispatcher.Invoke(ApplyThemeToLogTextBox);
    }

    private void ApplyThemeToLogTextBox()
    {
        var bgBrush = _resourceOwner.TryFindResource("SolidBackgroundFillColorBaseBrush") as SolidColorBrush;
        var fgBrush = _resourceOwner.TryFindResource("TextFillColorPrimaryBrush") as SolidColorBrush;

        System.Drawing.Color? back = null;
        System.Drawing.Color? fore = null;

        if (bgBrush is not null)
        {
            var c = bgBrush.Color;
            back = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
            _winFormsTextBox.BackColor = back.Value;
        }
        if (fgBrush is not null)
        {
            var c = fgBrush.Color;
            fore = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }

        // Improve readability for light themes: fallback to Black if the selected foreground is too light
        if (back is { } b)
        {
            var isDark = IsDark(b);
            if (fore is { } f)
            {
                if (!isDark && IsTooLight(f))
                {
                    f = System.Drawing.Color.Black;
                }
                _winFormsTextBox.ForeColor = f;
                _currentForeColor = f;
                // In light mode, enforce readable mono color over any fragment coloring produced by the sink
                _forceMonoOnLight = !isDark;
                if (_forceMonoOnLight)
                {
                    ForceRichTextForeColor(f);
                }
                else
                {
                    _forceMonoOnLight = false;
                }
            }

            // Try to match scrollbars to theme (Win11/10) via Immersive Dark Mode for dark;
            // use Explorer theme in light for modern scrollbars (handled inside helper)
            Win32DarkMode.SetImmersiveDarkMode(_winFormsTextBox.Handle, isDark);
        }
    }
    
    private static bool IsDark(System.Drawing.Color c)
    {
        var lum = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        return lum < 0.5;
    }

    private static bool IsTooLight(System.Drawing.Color c)
    {
        var lum = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        return lum > 0.75; // very light
    }
    
    private void ForceRichTextForeColor(System.Drawing.Color color)
    {
        try
        {
            _winFormsTextBox.SuspendLayout();
            var savedStart = _winFormsTextBox.SelectionStart;
            var savedLength = _winFormsTextBox.SelectionLength;
            _winFormsTextBox.SelectAll();
            _winFormsTextBox.SelectionColor = color;
            _winFormsTextBox.SelectionStart = savedStart;
            _winFormsTextBox.SelectionLength = savedLength;
        }
        finally
        {
            _winFormsTextBox.ResumeLayout();
        }
    }

    private void OnWinFormsTextChanged(object? sender, EventArgs e)
    {
        if (_forceMonoOnLight)
        {
            ForceRichTextForeColor(_currentForeColor);
        }
    }

    [RelayCommand] private void Clear()
    {
        CloseAndFlush();
        Initialized();
    }
    
    [RelayCommand] private static void ClearGeometry()
    {
        VisualizationController.Clear();
    }

    public void Dispose()
    {
        ApplicationThemeManager.Changed -= OnThemeChanged;
        _winFormsTextBox.TextChanged -= OnWinFormsTextChanged;
        _consoleRedirector.Dispose();
        GC.SuppressFinalize(this);
    }

    public void RefreshTheme()
    {
        ApplyThemeToLogTextBox();
    }
}

internal static class Win32DarkMode
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

    public static void SetImmersiveDarkMode(IntPtr hwnd, bool enable)
    {
        if (hwnd == IntPtr.Zero) return;
        var useDark = enable ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDark, sizeof(int));
        try
        {
            if (enable)
                SetWindowTheme(hwnd, "DarkMode_Explorer", null);
            else
                SetWindowTheme(hwnd, "Explorer", null);
        }
        catch
        {
            // ignore if not supported
        }
    }
}
using System.Diagnostics;
using System.Globalization;
using RevitDevTool.Models;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Windows.Forms.Integration;
using RevitDevTool.Theme;
using Serilog.Sinks.RichTextBoxForms;
using Wpf.Ui.Appearance;

namespace RevitDevTool.ViewModel;

internal partial class TraceLogViewModel : ObservableObject, IDisposable
{
    public WindowsFormsHost LogTextBox { get; }

    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly ConsoleRedirector _consoleRedirector;
    
    private SerilogTraceListener? _traceListener;
    private RichTextBoxSink? _sink;
    private Logger? _logger;
    
    private readonly RichTextBox _winFormsTextBox;
    private static bool IsDarkTheme => ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;

    [ObservableProperty] private bool _isStarted = true;
    [ObservableProperty] private LogEventLevel _logLevel = LogEventLevel.Debug;

    partial void OnLogLevelChanged(LogEventLevel value)
    {
        _levelSwitch.MinimumLevel = value;
    }

    partial void OnIsStartedChanged(bool value)
    {
        TraceStatus(value);
        _winFormsTextBox.Clear();
    }
    
    private void TraceStatus(bool isStarted)
    {
        if (isStarted)
        {
            Initialized();
            if (_traceListener != null) Trace.Listeners.Add(_traceListener);
            Trace.Listeners.Add(TraceGeometry.TraceListener);
            VisualizationController.Start();
        }
        else
        {
            if (_traceListener != null) Trace.Listeners.Remove(_traceListener);
            Trace.Listeners.Remove(TraceGeometry.TraceListener);
            VisualizationController.Stop();
            CloseAndFlush();
        }
    }

    private void Initialized()
    {
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .WriteTo.RichTextBox(_winFormsTextBox,
                out _sink,
                maxLogLines: int.MaxValue,
                formatProvider: CultureInfo.InvariantCulture,
                theme: IsDarkTheme ? AdaptiveThemePresets.EnhancedDark : AdaptiveThemePresets.EnhancedLight,
                autoScroll: true);

        _logger ??= loggerConfig.CreateLogger();
        _traceListener ??= new SerilogTraceListener(_logger);
    }

    private void CloseAndFlush()
    {
        _logger?.Dispose();
        _logger = null;
        _traceListener?.Dispose();
        _traceListener = null;
    }
    
    public TraceLogViewModel()
    {
        _winFormsTextBox = new RichTextBox
        {
            Font = new Font("Cascadia Mono", 9f, FontStyle.Regular, GraphicsUnit.Point),
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
        
        PresentationTraceSources.ResourceDictionarySource.Switch.Level = SourceLevels.Critical;
        _levelSwitch = new LoggingLevelSwitch(_logLevel);
        _consoleRedirector = new ConsoleRedirector();
        
        ApplicationThemeManager.Changed += OnThemeChanged;
        TraceStatus(IsStarted);
    }

    private void OnThemeChanged(ApplicationTheme theme, System.Windows.Media.Color accent)
    {
        LogTextBox.Dispatcher.Invoke(() =>
        {
            ApplyLogTheme();
            
            if (IsStarted)
            {
                RestartLogging();
            }
        });
    }

    private void RestartLogging()
    {
        CloseAndFlush();
        ApplyLogTheme();
        TraceStatus(IsStarted);
    }

    private void ApplyLogTheme()
    {
        _winFormsTextBox.BackColor = IsDarkTheme 
            ? System.Drawing.Color.FromArgb(30, 30, 30) 
            : System.Drawing.Color.FromArgb(250, 250, 250);
        Win32DarkMode.SetImmersiveDarkMode(_winFormsTextBox.Handle, IsDarkTheme);
    }

    [RelayCommand] private void Clear()
    {
        _winFormsTextBox.Clear();
        _sink?.Clear();
    }
    
    [RelayCommand] private static void ClearGeometry()
    {
        VisualizationController.Clear();
    }
    
    [RelayCommand] private static void OpenSettings()
    {
        var settingsWindow = new View.Settings();
        settingsWindow.ShowDialog();
    }

    public void Dispose()
    {
        ApplicationThemeManager.Changed -= OnThemeChanged;
        
        if (_traceListener != null)
        {
            Trace.Listeners.Remove(_traceListener);
            Trace.Listeners.Remove(TraceGeometry.TraceListener);
        }
        
        _logger?.Dispose();
        _logger = null;
        _traceListener?.Dispose();
        _traceListener = null;
        
        _consoleRedirector.Dispose();
        GC.SuppressFinalize(this);
    }

    public void RefreshTheme()
    {
        ApplyLogTheme();
        
        if (IsStarted)
        {
            RestartLogging();
        }
    }
}

internal static class Win32DarkMode
{
    // ReSharper disable InconsistentNaming
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    public static void SetImmersiveDarkMode(IntPtr hwnd, bool enable)
    {
        if (hwnd == IntPtr.Zero) return;
        var useDark = enable ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDark, sizeof(int));
        try
        {
            SetWindowTheme(hwnd, enable ? "DarkMode_Explorer" : "Explorer", null);
        }
        catch
        {
            // ignore
        }
    }
}
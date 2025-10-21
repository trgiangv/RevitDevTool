using System.Diagnostics;
using System.Globalization;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Windows.Forms.Integration;
using RevitDevTool.Models.Trace ;
using RevitDevTool.Theme;
using RevitDevTool.View.Settings ;
using ricaun.Revit.UI ;
using Serilog.Sinks.RichTextBoxForms;
using Wpf.Ui.Appearance;
using FontStyle = System.Drawing.FontStyle ;

namespace RevitDevTool.ViewModel;

internal partial class TraceLogViewModel : ObservableObject, IDisposable
{
    public WindowsFormsHost LogTextBox { get; }
    private readonly RichTextBox _winFormsTextBox;

    private readonly LoggingLevelSwitch _levelSwitch;
    private RichTextBoxSink? _sink;
    private Logger? _logger;

    private SerilogTraceListener? _traceListener;
    private TraceGeometry.TraceGeometryListener? _geometryListener;
    private readonly ConsoleRedirector _consoleRedirector;
    
    private ApplicationTheme _logTextBoxTheme;
    
    private static bool IsDarkTheme
    {
        get => ThemeWatcher.GetRequiredTheme() == ApplicationTheme.Dark ;
    }

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(OpenSettingsCommand))] private bool _isSettingOpened;
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
            if (_geometryListener != null) Trace.Listeners.Add(_geometryListener);
            VisualizationController.Start();
        }
        else
        {
            if (_traceListener != null) Trace.Listeners.Remove(_traceListener);
            if (_geometryListener != null) Trace.Listeners.Remove(_geometryListener);
            VisualizationController.Stop();
            CloseAndFlush();
        }
    }

    private void Initialized()
    {
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .WriteTo.RichTextBox(_winFormsTextBox, out _sink, 
                maxLogLines: int.MaxValue, 
                formatProvider: CultureInfo.InvariantCulture, 
                theme: IsDarkTheme ? AdaptiveThemePresets.EnhancedDark : AdaptiveThemePresets.EnhancedLight, 
                autoScroll: true);

        _logger ??= loggerConfig.CreateLogger();
        _traceListener ??= new SerilogTraceListener(_logger);
        _geometryListener ??= new TraceGeometry.TraceGeometryListener();
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

        TraceStatus(IsStarted);
        Context.UiApplication.Idling += OnIdling;
        ApplicationThemeManager.Changed += OnThemeChanged;
    }
    
    private void OnIdling(object? sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
    {
        var listeners = Trace.Listeners;
        if (_traceListener != null && !listeners.Contains( _traceListener)) 
            listeners.Add( _traceListener );

        if (_geometryListener != null && !listeners.Contains(_geometryListener))
            listeners.Add( _geometryListener );
    }

    private void OnThemeChanged(ApplicationTheme theme, System.Windows.Media.Color accent)
    {
        if (_logTextBoxTheme == theme) return;
        LogTextBox.Dispatcher.Invoke(() =>
        {
            ApplyLogTheme();
            _logTextBoxTheme = theme;

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
        _logTextBoxTheme = ThemeWatcher.GetRequiredTheme();
    }

    [RelayCommand]
    private void Clear()
    {
        _winFormsTextBox.Clear();
        _sink?.Clear();
    }

    [RelayCommand]
    private static void ClearGeometry()
    {
        VisualizationController.Clear();
    }

    [RelayCommand(CanExecute = nameof(CanOpenSettings))]
    private void OpenSettings()
    {
        var settingsWindow = new View.SettingsView();
        settingsWindow.SetAutodeskOwner();
        settingsWindow.Show();
        settingsWindow.NavigationService.Navigate(typeof(GeneralSettingsView));
        IsSettingOpened = true;
        settingsWindow.Closed += (_, _) => { IsSettingOpened = false ; };
    }

    private bool CanOpenSettings() => !IsSettingOpened;

    public void RefreshTheme()
    {
        ApplyLogTheme();

        if (IsStarted)
        {
            RestartLogging();
        }
    }

    public void Dispose()
    {
        ApplicationThemeManager.Changed -= OnThemeChanged;
        Context.UiApplication.Idling -= OnIdling;

        if (_traceListener != null) Trace.Listeners.Remove(_traceListener);
        if (_geometryListener != null) Trace.Listeners.Remove(_geometryListener);

        _logger?.Dispose();
        _logger = null;
        _traceListener?.Dispose();
        _traceListener = null;

        _consoleRedirector.Dispose();
        GC.SuppressFinalize(this);
    }
}
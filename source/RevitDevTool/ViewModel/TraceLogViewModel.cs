using System.Diagnostics;
using System.Globalization;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Windows.Forms.Integration;
using Autodesk.Revit.UI.Events;
using RevitDevTool.Commands;
using RevitDevTool.Models.Trace;
using RevitDevTool.Theme;
using RevitDevTool.View.Settings;
using ricaun.Revit.UI;
using Serilog.Sinks.RichTextBoxForms;
using Wpf.Ui.Appearance;
using FontStyle = System.Drawing.FontStyle;

namespace RevitDevTool.ViewModel;

internal partial class TraceLogViewModel : ObservableObject, IDisposable
{
    private readonly RichTextBox _winFormsTextBox;
    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly ThemeChangedEvent _onThemeChangedHandler;
    private readonly EventHandler<IdlingEventArgs> _onIdlingHandler;

    private RichTextBoxSink? _sink;
    private Logger? _logger;
    private SerilogTraceListener? _traceListener;
    private TraceGeometry.TraceGeometryListener? _geometryListener;
    private TraceEventNotifier? _traceEventNotifier;
    private ConsoleRedirector? _consoleRedirector;

    private ApplicationTheme _currentTheme;

    public WindowsFormsHost LogTextBox { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenSettingsCommand))]
    private bool _isSettingOpened;

    [ObservableProperty]
    private bool _isStarted = true;

    private bool _isSubcribe;
    private bool _isClearing;

    [ObservableProperty]
    private LogEventLevel _logLevel = LogEventLevel.Debug;

    partial void OnLogLevelChanged(LogEventLevel value)
    {
        _levelSwitch.MinimumLevel = value;
    }

    partial void OnIsStartedChanged(bool value)
    {
        if (value) StartTracing();
        else StopTracing();
    }

    private void StartTracing()
    {
        InitializeLogger();
        RegisterTraceListeners();
        VisualizationController.Start();
    }

    private void StopTracing()
    {
        UnregisterTraceListeners();
        VisualizationController.Stop();
        DisposeLogger();
        ClearGeometry();
    }

    private void InitializeLogger()
    {
        var textTheme = _currentTheme == ApplicationTheme.Dark ? AdaptiveThemePresets.EnhancedDark : AdaptiveThemePresets.EnhancedLight;

        var loggerConfig = new LoggerConfiguration().MinimumLevel.ControlledBy(_levelSwitch).WriteTo.RichTextBox(_winFormsTextBox, out _sink, maxLogLines: int.MaxValue, formatProvider: CultureInfo.InvariantCulture, theme: textTheme, autoScroll: true);

        _logger ??= loggerConfig.CreateLogger();
        _traceListener ??= new SerilogTraceListener(_logger);
        _geometryListener ??= new TraceGeometry.TraceGeometryListener();
        _traceEventNotifier ??= new TraceEventNotifier();
    }

    private void DisposeLogger()
    {
        _winFormsTextBox.Clear();
        _sink?.Dispose();
        _sink = null;
        _logger?.Dispose();
        _logger = null;
        _traceListener?.Dispose();
        _traceListener = null;
        _geometryListener?.Dispose();
        _geometryListener = null;
        _traceEventNotifier?.Dispose();
        _traceEventNotifier = null;
    }

    private void RegisterTraceListeners()
    {
        if (_traceListener != null && !Trace.Listeners.Contains(_traceListener))
            Trace.Listeners.Add(_traceListener);

        if (_geometryListener != null && !Trace.Listeners.Contains(_geometryListener))
            Trace.Listeners.Add(_geometryListener);
        
        if (_traceEventNotifier != null && !Trace.Listeners.Contains(_traceEventNotifier))
            Trace.Listeners.Add(_traceEventNotifier);
    }

    private void UnregisterTraceListeners()
    {
        if (_traceListener != null)
            Trace.Listeners.Remove(_traceListener);

        if (_geometryListener != null)
            Trace.Listeners.Remove(_geometryListener);
        
        if (_traceEventNotifier != null)
            Trace.Listeners.Remove(_traceEventNotifier);
    }

    private void ApplyTextBoxTheme()
    {
        var isDark = _currentTheme == ApplicationTheme.Dark;
        
        _winFormsTextBox.BackColor = isDark ? System.Drawing.Color.FromArgb(30, 30, 30) : System.Drawing.Color.FromArgb(250, 250, 250);

        Win32DarkMode.SetImmersiveDarkMode(_winFormsTextBox.Handle, isDark);
    }

    private void OnThemeChanged(ApplicationTheme theme, System.Windows.Media.Color accent)
    {
        if (_currentTheme == theme) return;
        UpdateTheme(theme, IsStarted);
    }

    private void UpdateTheme(ApplicationTheme theme, bool shouldRestart)
    {
        LogTextBox.Dispatcher.Invoke(() =>
        {
            _currentTheme = theme;
            ApplyTextBoxTheme();
            if (shouldRestart)
            {
                RestartLogging();
            }
        });
    }

    private void RestartLogging()
    {
        UnregisterTraceListeners();
        DisposeLogger();
        InitializeLogger();
        RegisterTraceListeners();
    }

    public void Subcribe()
    {
        if (_isSubcribe) return;

        _consoleRedirector ??= new ConsoleRedirector();

        UpdateTheme(ThemeWatcher.GetRequiredTheme(), true);
        IsStarted = true;

        Context.UiApplication.Idling += _onIdlingHandler;
        ApplicationThemeManager.Changed += _onThemeChangedHandler;

        _isSubcribe = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubcribe) return;

        Context.UiApplication.Idling -= _onIdlingHandler;
        ApplicationThemeManager.Changed -= _onThemeChangedHandler;

        _isSubcribe = false;
    }

    private void OnIdling(object? sender, IdlingEventArgs e)
    {
        if (IsStarted && !_isClearing)
        {
            RegisterTraceListeners();
        }
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

        LogTextBox.Loaded += (_, _) =>
        {
            ApplyTextBoxTheme();
        };

        PresentationTraceSources.ResourceDictionarySource.Switch.Level = SourceLevels.Critical;
        _levelSwitch = new LoggingLevelSwitch(_logLevel);
        _onThemeChangedHandler = OnThemeChanged;
        _onIdlingHandler = OnIdling;
        _currentTheme = ThemeWatcher.GetRequiredTheme();

        Subcribe();
        StartTracing();
    }

    [RelayCommand]
    private void Clear()
    {
        _isClearing = true;
        if (IsStarted)
        {
            UnregisterTraceListeners();
            DisposeLogger();
            InitializeLogger();
            RegisterTraceListeners();
        }
        _isClearing = false;
    }

    [RelayCommand]
    private static void ClearGeometry()
    {
        VisualizationController.Clear();
    }

    [RelayCommand(CanExecute = nameof(CanOpenSettings))]
    private void OpenSettings()
    {
        var settingsWindow = new View.SettingsWindow();
        if (TraceCommand.FloatingWindow != null)
        {
            settingsWindow.Owner = TraceCommand.FloatingWindow;
        }
        else
        {
            settingsWindow.SetAutodeskOwner();
        }
        settingsWindow.Show();
        settingsWindow.NavigationService.Navigate(typeof(GeneralSettingsView));
        IsSettingOpened = true;
        settingsWindow.Closed += (_, _) => { IsSettingOpened = false; };
    }

    private bool CanOpenSettings() => !IsSettingOpened;

    public void Dispose()
    {
        IsStarted = false;
        StopTracing();
        Unsubscribe();

        _consoleRedirector?.Dispose();
        _consoleRedirector = null;

        GC.SuppressFinalize(this);
    }
}
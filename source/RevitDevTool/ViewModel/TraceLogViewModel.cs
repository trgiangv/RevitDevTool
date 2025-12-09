using Serilog.Core;
using Serilog.Events;
using System.Windows.Forms.Integration;
using Autodesk.Revit.UI.Events;
using RevitDevTool.Commands;
using RevitDevTool.Models.Trace;
using RevitDevTool.Services;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using RevitDevTool.View.Settings;
using ricaun.Revit.UI;
using Serilog.Sinks.RichTextBoxForms;
using Wpf.Ui.Appearance;
using Color = System.Windows.Media.Color;
using FontStyle = System.Drawing.FontStyle;

namespace RevitDevTool.ViewModel;

internal partial class TraceLogViewModel : ObservableObject, IDisposable
{
    private readonly RichTextBox _winFormsTextBox;
    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly ThemeChangedEvent _onThemeChangedHandler;
    private readonly EventHandler<IdlingEventArgs> _onIdlingHandler;
    private readonly Action _onSettingsChangedHandler;

    private RichTextBoxSink? _sink;
    private Logger? _logger;
    private SerilogTraceListener? _traceListener;
    private GeometryListener? _geometryListener;
    private NotifyListener? _traceEventNotifier;
    private ConsoleRedirector? _consoleRedirector;
    private ApplicationTheme _currentTheme;
    private bool _isSubscribe;
    private bool _isClearing;

    public WindowsFormsHost LogTextBox { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenSettingsCommand))]
    private bool _isSettingOpened;

    [ObservableProperty]
    private bool _isStarted = true;

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

    #region Tracing Management

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
        var logConfig = SettingsService.Instance.LogConfig;
        var loggerConfig = LoggerConfigUtils.BuildLoggerConfiguration(_levelSwitch, _winFormsTextBox, textTheme, logConfig, out _sink);

        _logger ??= loggerConfig.CreateLogger();
        _traceListener ??= new SerilogTraceListener(_logger, logConfig.IncludeStackTrace, logConfig.StackTraceDepth);
        _geometryListener ??= new GeometryListener();
        _traceEventNotifier ??= new NotifyListener();
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
        TraceUtils.RegisterTraceListeners(_traceListener, _geometryListener, _traceEventNotifier);
    }

    private void UnregisterTraceListeners()
    {
        TraceUtils.UnregisterTraceListeners(_traceListener, _geometryListener, _traceEventNotifier);
    }

    private void RestartLogging()
    {
        UnregisterTraceListeners();
        DisposeLogger();
        InitializeLogger();
        RegisterTraceListeners();
    }

    #endregion

    #region Theme Management

    private void ApplyTextBoxTheme()
    {
        var isDark = _currentTheme == ApplicationTheme.Dark;
        
        _winFormsTextBox.BackColor = isDark ? System.Drawing.Color.FromArgb(30, 30, 30) : System.Drawing.Color.FromArgb(250, 250, 250);

        Win32Utils.SetImmersiveDarkMode(_winFormsTextBox.Handle, isDark);
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

    #endregion

    #region Event Subscription

    public void Subscribe()
    {
        if (_isSubscribe) return;

        _consoleRedirector ??= new ConsoleRedirector();

        UpdateTheme(ThemeWatcher.GetRequiredTheme(), true);
        IsStarted = true;

        Context.UiApplication.Idling += _onIdlingHandler;
        ApplicationThemeManager.Changed += _onThemeChangedHandler;
        SettingsService.Instance.LogSettingsChanged += _onSettingsChangedHandler;

        _isSubscribe = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribe) return;

        Context.UiApplication.Idling -= _onIdlingHandler;
        ApplicationThemeManager.Changed -= _onThemeChangedHandler;
        SettingsService.Instance.LogSettingsChanged -= _onSettingsChangedHandler;

        _isSubscribe = false;
    }
    
    private void OnIdling(object? sender, IdlingEventArgs e)
    {
        if (IsStarted && !_isClearing)
        {
            RegisterTraceListeners();
        }
    }
    
    private void OnThemeChanged(ApplicationTheme theme, Color accent)
    {
        if (_currentTheme == theme) return;
        UpdateTheme(theme, IsStarted);
    }
    
    private void OnSettingsChanged()
    {
        if (!IsStarted) return;
        RestartLogging();
    }

    #endregion

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

        _levelSwitch = new LoggingLevelSwitch(_logLevel);
        _onThemeChangedHandler = OnThemeChanged;
        _onIdlingHandler = OnIdling;
        _onSettingsChangedHandler = OnSettingsChanged;
        _currentTheme = ThemeWatcher.GetRequiredTheme();

        Subscribe();
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
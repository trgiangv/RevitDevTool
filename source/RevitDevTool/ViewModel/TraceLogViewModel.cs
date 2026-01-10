using System.Windows.Forms.Integration;
using Autodesk.Revit.UI.Events;
using CommunityToolkit.Mvvm.Messaging;
using RevitDevTool.Logging;
using RevitDevTool.Logging.Serilog;
using RevitDevTool.Messages;
using RevitDevTool.Models.Trace;
using RevitDevTool.Services;
using RevitDevTool.Theme;
using Serilog.Events;

namespace RevitDevTool.ViewModel;

public partial class TraceLogViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;
    private readonly RichTextBoxSink _outputSink;
    private readonly WeakReferenceMessenger _messenger;
    private readonly EventHandler<EventArgs> _onThemeChangedHandler;
    private readonly EventHandler<IdlingEventArgs> _onIdlingHandler;

    private ConsoleRedirector? _consoleRedirector;
    private AppTheme _currentTheme;
    private bool _isSubscribed;
    private bool _isClearing;

    public WindowsFormsHost LogTextBox => (WindowsFormsHost)_outputSink.GetHostControl();

    [ObservableProperty]
    private bool _isStarted = true;

    [ObservableProperty]
    private LogEventLevel _logLevel = LogEventLevel.Debug;

    partial void OnLogLevelChanged(LogEventLevel value)
    {
        _loggingService.SetMinimumLevel(ToLogLevel(value));
    }

    partial void OnIsStartedChanged(bool value)
    {
        if (value) StartTracing();
        else StopTracing();
    }

    public TraceLogViewModel(ISettingsService settingsService, ITraceListenerFactory traceListenerFactory, ILoggerFactory loggerFactory)
    {
        _settingsService = settingsService;
        _messenger = WeakReferenceMessenger.Default;
        _outputSink = new RichTextBoxSink();
        _loggingService = new LoggingService(settingsService, loggerFactory, traceListenerFactory, _outputSink);
        _onThemeChangedHandler = OnThemeChanged;
        _onIdlingHandler = OnIdling;
        _currentTheme = ThemeManager.Current.ActualApplicationTheme;
        Subscribe();
        StartTracing();
    }

    private void StartTracing()
    {
        _loggingService.Initialize(IsDarkTheme);
        _loggingService.RegisterTraceListeners();
        VisualizationController.Start();
    }

    private void StopTracing()
    {
        _loggingService.UnregisterTraceListeners();
        VisualizationController.Stop();
        _loggingService.ClearOutput();
        ClearGeometry();
    }

    private void UpdateTheme(AppTheme theme, bool shouldRestart)
    {
        LogTextBox.Dispatcher.Invoke(() =>
        {
            _currentTheme = theme;
            _outputSink.SetTheme(IsDarkTheme);
            if (shouldRestart && IsStarted)
            {
                _loggingService.Restart(IsDarkTheme);
            }
        });
    }

    private bool IsDarkTheme => _currentTheme == AppTheme.Dark;

    public void Subscribe()
    {
        if (_isSubscribed) return;

        _consoleRedirector ??= new ConsoleRedirector();

        UpdateTheme(ThemeManager.Current.ActualApplicationTheme, true);
        IsStarted = true;

        Context.UiApplication.Idling += _onIdlingHandler;
        ThemeManager.Current.ActualApplicationThemeChanged += _onThemeChangedHandler;
        _messenger.Register<TraceLogViewModel, LogSettingsAppliedMessage>(
            this,
            static (r, _) => r.OnSettingsApplied()
        );

        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed) return;

        Context.UiApplication.Idling -= _onIdlingHandler;
        ThemeManager.Current.ActualApplicationThemeChanged -= _onThemeChangedHandler;
        _messenger.UnregisterAll(this);

        _isSubscribed = false;
    }

    private void OnIdling(object? sender, IdlingEventArgs e)
    {
        if (IsStarted && !_isClearing)
        {
            _loggingService.RegisterTraceListeners();
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        var theme = ThemeManager.Current.ActualApplicationTheme;
        if (_currentTheme == theme) return;
        UpdateTheme(theme, IsStarted);
    }

    private void OnSettingsApplied()
    {
        if (!IsStarted) return;
        LogLevel = _settingsService.LogConfig.LogLevel;
        _loggingService.Restart(IsDarkTheme);
    }

    [RelayCommand]
    private void Clear()
    {
        _isClearing = true;
        if (IsStarted)
        {
            _loggingService.Restart(IsDarkTheme);
        }
        _isClearing = false;
    }

    [RelayCommand]
    private static void ClearGeometry()
    {
        VisualizationController.Clear();
    }

    public void Dispose()
    {
        IsStarted = false;
        Unsubscribe();
        
        _loggingService.Dispose();
        _consoleRedirector?.Dispose();
        _consoleRedirector = null;

        GC.SuppressFinalize(this);
    }

    private static LogLevel ToLogLevel(LogEventLevel serilogLevel) => serilogLevel switch
    {
        LogEventLevel.Verbose => Logging.LogLevel.Verbose,
        LogEventLevel.Debug => Logging.LogLevel.Debug,
        LogEventLevel.Information => Logging.LogLevel.Information,
        LogEventLevel.Warning => Logging.LogLevel.Warning,
        LogEventLevel.Error => Logging.LogLevel.Error,
        LogEventLevel.Fatal => Logging.LogLevel.Fatal,
        _ => Logging.LogLevel.Debug
    };
}

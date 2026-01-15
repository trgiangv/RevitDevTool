using Autodesk.Revit.UI.Events;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using RevitDevTool.Controllers;
using RevitDevTool.Listeners;
using RevitDevTool.Logging;
using RevitDevTool.Settings;
using RevitDevTool.Theme;
using RevitDevTool.ViewModel.Messages;
using System.Windows.Forms.Integration;

namespace RevitDevTool.ViewModel;

public partial class TraceLogViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;
    private readonly WeakReferenceMessenger _messenger;
    private readonly EventHandler<EventArgs> _onThemeChangedHandler;
    private readonly EventHandler<IdlingEventArgs> _onIdlingHandler;

    private ConsoleRedirector? _consoleRedirector;
    private AppTheme _currentTheme;
    private bool _isSubscribed;
    private bool _isClearing;

    public WindowsFormsHost? LogTextBox => _loggingService.OutputSink?.GetHostControl() as WindowsFormsHost;

    [ObservableProperty]
    private bool _isStarted = true;

    [ObservableProperty]
    private LogLevel _logLevel = LogLevel.Debug;

    partial void OnLogLevelChanged(LogLevel value)
    {
        _loggingService.SetMinimumLevel(value);
    }

    partial void OnIsStartedChanged(bool value)
    {
        if (value) StartTracing();
        else StopTracing();
    }

    public TraceLogViewModel(ISettingsService settingsService, ILoggingService loggingService)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
        _messenger = WeakReferenceMessenger.Default;
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
        LogTextBox?.Dispatcher.Invoke(() =>
        {
            _currentTheme = theme;
            _loggingService.OutputSink?.SetTheme(IsDarkTheme);
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
}

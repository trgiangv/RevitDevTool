using Autodesk.Revit.UI.Events;
using CommunityToolkit.Mvvm.Messaging;
using RevitDevTool.Controllers;
using RevitDevTool.Settings;
using RevitDevTool.ViewModel.Messages;
using System.Windows.Forms.Integration;
using RevitDevTool.Logging;
using RevitDevTool.Logging.Listeners;
using Serilog.Events;

namespace RevitDevTool.ViewModel;

public sealed partial class TraceLogViewModel : ObservableObject, IDisposable,
    IRecipient<GeometryCountChangedMessage>,
    IRecipient<LogSettingsAppliedMessage>
{
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;
    private readonly WeakReferenceMessenger _messenger;
    private readonly EventHandler<IdlingEventArgs> _onIdlingHandler;

    private ConsoleRedirector? _consoleRedirector;
    private bool _isSubscribed;
    private bool _isClearing;

    public WindowsFormsHost? LogTextBox => _loggingService.Monitor?.GetHostControl() as WindowsFormsHost;

    [ObservableProperty]
    private bool _isStarted;

    [ObservableProperty]
    private LogEventLevel _logLevel = LogEventLevel.Debug;

    [ObservableProperty]
    private int _geometryCount;

    partial void OnLogLevelChanged(LogEventLevel value)
    {
        _loggingService.SetMinimumLevel(value);
    }

    partial void OnIsStartedChanged(bool value)
    {
        _settingsService.GeneralConfig.IsTraceEnabled = value;
        if (value) StartTracing();
        else StopTracing();
    }

    public TraceLogViewModel(ISettingsService settingsService, ILoggingService loggingService)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
        _messenger = WeakReferenceMessenger.Default;
        _onIdlingHandler = OnIdling;
        _isStarted = _settingsService.GeneralConfig.IsTraceEnabled;
        Subscribe();
        if (_isStarted) StartTracing();
    }

    private void StartTracing()
    {
        _loggingService.Initialize();
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

    public void Subscribe()
    {
        if (_isSubscribed) return;

        _consoleRedirector ??= new ConsoleRedirector();

        Context.UiApplication.Idling += _onIdlingHandler;
        _messenger.RegisterAll(this);

        _isSubscribed = true;
    }

    public void Receive(GeometryCountChangedMessage message)
    {
        GeometryCount = message.Value;
    }

    public void Receive(LogSettingsAppliedMessage message)
    {
        if (!IsStarted) return;
        LogLevel = _settingsService.LogConfig.LogLevel;
        _loggingService.Restart();
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed) return;

        Context.UiApplication.Idling -= _onIdlingHandler;
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
    
    [RelayCommand]
    private void Clear()
    {
        _isClearing = true;
        if (IsStarted)
        {
            _loggingService.Restart();
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
    }
}

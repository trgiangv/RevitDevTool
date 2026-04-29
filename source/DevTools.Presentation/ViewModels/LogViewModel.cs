using CommunityToolkit.Mvvm.Messaging;
using DevTools.Presentation.Interfaces;
using DevTools.Presentation.ViewModels.Messages;
using Microsoft.Extensions.Logging;
namespace DevTools.Presentation.ViewModels;

public sealed partial class LogViewModel : ObservableObject, IDisposable,
    IRecipient<GeometryCountChangedMessage>,
    IRecipient<LogSettingsAppliedMessage>
{
    private readonly IDevToolsSettingsService _settingsService;
    private readonly IDevToolsLoggingService _loggingService;
    private readonly IMessenger _messenger;
    private readonly IVisualizationBridge? _visualization;
    private readonly IHostIdlingBridge? _idling;

    private bool _isSubscribed;

    public System.Windows.FrameworkElement? LogTextBox => _loggingService.HostElement;

    public bool HasVisualization => _visualization != null;

    [ObservableProperty] private bool _isStarted;
    [ObservableProperty] private LogLevel _logLevel = LogLevel.Debug;
    [ObservableProperty] private int _geometryCount;

    partial void OnLogLevelChanged(LogLevel value) => _loggingService.SetMinimumLevel(value);

    partial void OnIsStartedChanged(bool value)
    {
        _settingsService.GeneralConfig.IsTraceEnabled = value;
        if (value) StartTracing();
        else StopTracing();
    }

    public LogViewModel(
        IDevToolsSettingsService settingsService,
        IDevToolsLoggingService loggingService,
        IMessenger messenger,
        IVisualizationBridge? visualization = null,
        IHostIdlingBridge? idling = null)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
        _messenger = messenger;
        _visualization = visualization;
        _idling = idling;
        _isStarted = _settingsService.GeneralConfig.IsTraceEnabled;
        if (_isStarted) StartTracing();
    }

    private void StartTracing()
    {
        _loggingService.Initialize();
        _loggingService.RegisterTraceListeners();
        _visualization?.Start();
    }

    private void StopTracing()
    {
        _loggingService.UnregisterTraceListeners();
        _visualization?.Stop();
        _loggingService.ClearOutput();
        ClearGeometry();
    }

    public void Subscribe()
    {
        if (_isSubscribed) return;
        _idling?.Subscribe(OnIdling);
        _messenger.RegisterAll(this);
        _isSubscribed = true;
    }

    public void Receive(GeometryCountChangedMessage message)
    {
        GeometryCount = message.Count;
    }

    public void Receive(LogSettingsAppliedMessage message)
    {
        if (!IsStarted) return;
        LogLevel = _settingsService.LogConfig.TraceListener.LogLevel;
        _loggingService.EnableTarget(message.Sink);
    }

    private void OnIdling()
    {
        if (IsStarted) _loggingService.RegisterTraceListeners();
    }

    [RelayCommand]
    private void Clear() => _loggingService.ClearOutput();

    [RelayCommand]
    private void ClearGeometry() => _visualization?.Clear();

    public void Dispose()
    {
        if (_isSubscribed)
        {
            _idling?.Unsubscribe(OnIdling);
            _messenger.UnregisterAll(this);
            _isSubscribed = false;
        }
        _loggingService.UnregisterTraceListeners();
        _visualization?.Stop();
        _loggingService.Dispose();
    }
}

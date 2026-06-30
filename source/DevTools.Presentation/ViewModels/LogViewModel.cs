using CommunityToolkit.Mvvm.Messaging;
using DevTools.Presentation.Interfaces;
using DevTools.Presentation.ViewModels.Messages;
using DevTools.Settings;
using Microsoft.Extensions.Logging;
namespace DevTools.Presentation.ViewModels;

public sealed partial class LogViewModel : ObservableObject, IDisposable,
    IRecipient<GeometryCountChangedMessage>,
    IRecipient<LogSettingsAppliedMessage>
{
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;
    private readonly IMessenger _messenger;
    private readonly IVisualizationBridge? _visualization;
    private readonly IHostIdlingBridge? _idling;

    private bool _isSubscribed;

    public System.Windows.FrameworkElement? LogTextBox => _loggingService.HostElement;

    public bool HasVisualization => _visualization != null;

    [ObservableProperty]
    public partial bool IsStarted { get; set; }

    [ObservableProperty]
    public partial LogLevel LogLevel { get; set; } = LogLevel.Debug;

    [ObservableProperty]
    public partial int GeometryCount { get; set; }

    partial void OnLogLevelChanged(LogLevel value) => _loggingService.SetMinimumLevel(value);

    partial void OnIsStartedChanged(bool value)
    {
        _settingsService.GeneralConfig.IsTraceEnabled = value;
        if (value) StartTracing();
        else StopTracing();
    }

    public LogViewModel(
        ISettingsService settingsService,
        ILoggingService loggingService,
        IMessenger messenger,
        IVisualizationBridge? visualization = null,
        IHostIdlingBridge? idling = null)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
        _messenger = messenger;
        _visualization = visualization;
        _idling = idling;
        IsStarted = _settingsService.GeneralConfig.IsTraceEnabled;
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
        if (IsStarted) StartTracing();
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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using DevTools.Logging;
using DevTools.Logging.Options;
using DevTools.Presentation;
using DevTools.Presentation.Interfaces;
using DevTools.Settings;
using DevTools.Presentation.ViewModels.Messages;
using Microsoft.Extensions.Logging;
using ZLogger.Providers;
// ReSharper disable UnusedParameterInPartialMethod

namespace DevTools.Presentation.ViewModels.Settings;

public partial class LogSettingsViewModel : ObservableObject, IDataErrorInfo, IRecipient<ResetSettingsMessage>
{
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;
    private readonly IMessenger _messenger;
    private readonly ILogEnricherProvider? _enricherProvider;

    public static LogLevel[] LogLevels { get; } = Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>().ToArray();
    public static RollingInterval[] LogTimeIntervals { get; } = Enum.GetValues(typeof(RollingInterval)).Cast<RollingInterval>().ToArray();
    public static SourceLevels[] SourceLevels { get; } = Enum.GetValues(typeof(SourceLevels)).Cast<SourceLevels>().ToArray();
    public static LogSink[] AvailableLogTargets { get; } = Enum.GetValues(typeof(LogSink)).Cast<LogSink>().ToArray();

    public bool HasEnrichers => _enricherProvider != null;
    public IReadOnlyList<object> AvailableEnrichers => _enricherProvider?.AvailableEnrichers ?? [];
    public ObservableCollection<object> SelectedEnrichers { get; } = [];

    [ObservableProperty]
    public partial LogLevel LogLevel { get; set; }

    [ObservableProperty]
    public partial bool EnableJson { get; set; }

    [ObservableProperty]
    public partial string InformationKeywords { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WarningKeywords { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorKeywords { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CriticalKeywords { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool RestartRequired { get; set; }

    [ObservableProperty]
    public partial bool IncludeStackTrace { get; set; }

    [ObservableProperty]
    public partial SourceLevels WpfTraceLevel { get; set; }

    [ObservableProperty]
    public partial bool IncludeWpfTrace { get; set; }

    [ObservableProperty]
    public partial RollingInterval TimeInterval { get; set; }

    [ObservableProperty]
    public partial int StackTraceDepth { get; set; }

    [ObservableProperty]
    public partial string LogFolder { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool AutoClean { get; set; }

    [ObservableProperty]
    public partial string HttpEndpoint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int HttpBatchSize { get; set; } = 100;
    public ObservableCollection<LogSink> SelectedLogTargets { get; } = [];

    public bool IsFileTargetSelected => SelectedLogTargets.Contains(LogSink.File);
    public bool IsHttpTargetSelected => SelectedLogTargets.Contains(LogSink.Http);
    public bool IsOutputSettingsVisible => IsFileTargetSelected || IsHttpTargetSelected;

    private Snapshot _baseline;

    public LogSettingsViewModel(
        ISettingsService settingsService,
        ILoggingService loggingService,
        IMessenger messenger,
        ILogEnricherProvider? enricherProvider = null)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
        _messenger = messenger;
        _enricherProvider = enricherProvider;

        SelectedLogTargets.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsFileTargetSelected));
            OnPropertyChanged(nameof(IsHttpTargetSelected));
            OnPropertyChanged(nameof(IsOutputSettingsVisible));
            _messenger.Send(new IsSaveLogChangedMessage(IsFileTargetSelected));
            UpdateHasPendingChanges();
        };

        LoadFromConfig();
        SetBaselineFromCurrent();
        _messenger.Register(this);
    }

    public string Error => string.Empty;

    public string this[string columnName] => columnName switch
    {
        nameof(InformationKeywords) => LogLevelDetector.ValidateKeywords(InformationKeywords) ?? string.Empty,
        nameof(WarningKeywords) => LogLevelDetector.ValidateKeywords(WarningKeywords) ?? string.Empty,
        nameof(ErrorKeywords) => LogLevelDetector.ValidateKeywords(ErrorKeywords) ?? string.Empty,
        nameof(CriticalKeywords) => LogLevelDetector.ValidateKeywords(CriticalKeywords) ?? string.Empty,
        _ => string.Empty
    };

    partial void OnLogLevelChanged(LogLevel value)
    {
        _settingsService.LogConfig.TraceListener.LogLevel = value;
        _loggingService.SetMinimumLevel(value);
    }

    partial void OnEnableJsonChanged(bool value)
    {
        _loggingService.SetPrettyJson(value);
        UpdateHasPendingChanges();
    }

    partial void OnIncludeStackTraceChanged(bool value) => UpdateHasPendingChanges();

    partial void OnWpfTraceLevelChanged(SourceLevels value)
    {
        _settingsService.LogConfig.TraceListener.WpfTraceLevel = value;
        PresentationTraceListenerHelper.ApplyPresentationTraceSwitches(value);
    }

    partial void OnIncludeWpfTraceChanged(bool value) => UpdateHasPendingChanges();
    partial void OnTimeIntervalChanged(RollingInterval value) => UpdateHasPendingChanges();
    partial void OnStackTraceDepthChanged(int value) => UpdateHasPendingChanges();
    partial void OnLogFolderChanged(string value) => UpdateHasPendingChanges();
    partial void OnAutoCleanChanged(bool value) => UpdateHasPendingChanges();
    partial void OnInformationKeywordsChanged(string value) => UpdateHasPendingChanges();
    partial void OnWarningKeywordsChanged(string value) => UpdateHasPendingChanges();
    partial void OnErrorKeywordsChanged(string value) => UpdateHasPendingChanges();
    partial void OnCriticalKeywordsChanged(string value) => UpdateHasPendingChanges();
    partial void OnHttpEndpointChanged(string value) => UpdateHasPendingChanges();
    partial void OnHttpBatchSizeChanged(int value) => UpdateHasPendingChanges();

    private void LoadFromConfig()
    {
        var config = _settingsService.LogConfig;

        LogLevel = config.TraceListener.LogLevel;
        EnableJson = config.FileLogging.Format == SaveFormat.Json;
        InformationKeywords = config.TraceListener.LevelKeys.Information;
        WarningKeywords = config.TraceListener.LevelKeys.Warning;
        ErrorKeywords = config.TraceListener.LevelKeys.Error;
        CriticalKeywords = config.TraceListener.LevelKeys.Critical;
        IncludeStackTrace = config.TraceListener.IncludeStackTrace;
        IncludeWpfTrace = config.TraceListener.IncludeWpfTrace;
        WpfTraceLevel = config.TraceListener.WpfTraceLevel;
        StackTraceDepth = config.TraceListener.StackTraceDepth;
        TimeInterval = config.FileLogging.RollingInterval;
        LogFolder = config.FileLogging.LogFolder;
        AutoClean = config.FileLogging.AutoClean;
        HttpEndpoint = config.HttpLogging.Endpoint;
        HttpBatchSize = config.HttpLogging.BatchSize;

        SelectedLogTargets.Clear();
        SelectedLogTargets.Add(LogSink.Monitor);
        if (config.FileLogging.Enabled) SelectedLogTargets.Add(LogSink.File);
        if (config.HttpLogging.Enabled) SelectedLogTargets.Add(LogSink.Http);

        SelectedEnrichers.Clear();
        if (_enricherProvider is not null)
        {
            foreach (var enricher in _enricherProvider.SelectedEnrichers)
                SelectedEnrichers.Add(enricher);
        }
    }

    public void Receive(ResetSettingsMessage message)
    {
        LoadFromConfig();
        SetBaselineFromCurrent();
    }

    private void SaveToConfig()
    {
        var config = _settingsService.LogConfig;
        var format = EnableJson ? SaveFormat.Json : SaveFormat.Text;

        config.TraceListener.LogLevel = LogLevel;
        config.Monitor.EnablePrettyJson = EnableJson;
        config.TraceListener.LevelKeys.Information = InformationKeywords;
        config.TraceListener.LevelKeys.Warning = WarningKeywords;
        config.TraceListener.LevelKeys.Error = ErrorKeywords;
        config.TraceListener.LevelKeys.Critical = CriticalKeywords;
        config.FileLogging.Enabled = IsFileTargetSelected;
        config.FileLogging.Format = format;
        config.TraceListener.IncludeStackTrace = IncludeStackTrace;
        config.TraceListener.IncludeWpfTrace = IncludeWpfTrace;
        config.TraceListener.WpfTraceLevel = WpfTraceLevel;
        config.TraceListener.StackTraceDepth = StackTraceDepth;
        config.FileLogging.RollingInterval = TimeInterval;
        config.FileLogging.LogFolder = LogFolder;
        config.FileLogging.AutoClean = AutoClean;
        config.HttpLogging.Enabled = IsHttpTargetSelected;
        config.HttpLogging.Endpoint = HttpEndpoint;
        config.HttpLogging.BatchSize = HttpBatchSize;
        config.HttpLogging.Format = format;

        if (_enricherProvider is not null)
            _enricherProvider.SelectedEnrichers = SelectedEnrichers.ToList();

        _settingsService.SaveSettings();
    }

    public void ApplyIfPendingChanges()
    {
        SaveToConfig();
        var changed = ComputeChangedTargets();
        SetBaselineFromCurrent();
        foreach (var target in changed)
            _messenger.Send(new LogSettingsAppliedMessage(target));
    }

    private List<LogSink> ComputeChangedTargets()
    {
        if (TraceListenerChanged())
            return [LogSink.Monitor, LogSink.File, LogSink.Http];

        var changed = new List<LogSink>();
        var outputChanged = OutputSettingsChanged();
        if (outputChanged) changed.Add(LogSink.Monitor);
        if (FileLoggingChanged() || outputChanged) changed.Add(LogSink.File);
        if (HttpLoggingChanged() || outputChanged) changed.Add(LogSink.Http);
        return changed;
    }

    private bool FileLoggingChanged() =>
        _baseline.IsFileEnabled != IsFileTargetSelected
        || _baseline.TimeInterval != TimeInterval
        || _baseline.AutoClean != AutoClean
        || !string.Equals(_baseline.LogFolder, LogFolder, StringComparison.OrdinalIgnoreCase);

    private bool HttpLoggingChanged() =>
        _baseline.IsHttpEnabled != IsHttpTargetSelected
        || _baseline.HttpEndpoint != HttpEndpoint
        || _baseline.HttpBatchSize != HttpBatchSize;

    private bool OutputSettingsChanged() => _baseline.EnableJson != EnableJson;

    private bool TraceListenerChanged() =>
        _baseline.IncludeStackTrace != IncludeStackTrace
        || _baseline.StackTraceDepth != StackTraceDepth
        || _baseline.IncludeWpfTrace != IncludeWpfTrace
        || _baseline.InformationKeywords != InformationKeywords
        || _baseline.WarningKeywords != WarningKeywords
        || _baseline.ErrorKeywords != ErrorKeywords
        || _baseline.CriticalKeywords != CriticalKeywords;

    private void SetBaselineFromCurrent()
    {
        _baseline = new Snapshot(
            IsFileTargetSelected, IsHttpTargetSelected, EnableJson,
            IncludeStackTrace, StackTraceDepth, IncludeWpfTrace,
            TimeInterval, LogFolder, AutoClean,
            InformationKeywords, WarningKeywords, ErrorKeywords, CriticalKeywords,
            HttpEndpoint, HttpBatchSize);
        RestartRequired = false;
    }

    private void UpdateHasPendingChanges()
    {
        RestartRequired = FileLoggingChanged() || HttpLoggingChanged() || OutputSettingsChanged() || TraceListenerChanged();
    }

    private readonly record struct Snapshot(
        bool IsFileEnabled, bool IsHttpEnabled, bool EnableJson,
        bool IncludeStackTrace, int StackTraceDepth, bool IncludeWpfTrace,
        RollingInterval TimeInterval, string LogFolder, bool AutoClean,
        string InformationKeywords, string WarningKeywords, string ErrorKeywords, string CriticalKeywords,
        string HttpEndpoint, int HttpBatchSize);

    [RelayCommand]
    private void BrowseFolder()
    {
        var selectedFolder = AppUtils.SelectFolder("Select Log Folder");
        if (!string.IsNullOrEmpty(selectedFolder))
            LogFolder = selectedFolder;
    }
}

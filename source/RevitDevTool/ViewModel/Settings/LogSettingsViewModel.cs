using CommunityToolkit.Mvvm.Messaging;
using DevTools.Logging;
using DevTools.Logging.Options;
using Microsoft.Extensions.Logging;
using RevitDevTool.Logging.Enums;
using RevitDevTool.Settings;
using RevitDevTool.Utils;
using RevitDevTool.ViewModel.Messages;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using RevitDevTool.Logging;
using ZLogger.Providers;

// ReSharper disable UnusedParameterInPartialMethod

namespace RevitDevTool.ViewModel.Settings;

public partial class LogSettingsViewModel : ObservableObject, IDataErrorInfo, IRecipient<ResetSettingsMessage>
{
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;
    private readonly IMessenger _messenger;

    public static LogLevel[] LogLevels { get; } = Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>().ToArray();
    public static SaveFormat[] LogSaveFormats { get; } = Enum.GetValues(typeof(SaveFormat)).Cast<SaveFormat>().ToArray();
    public static RollingInterval[] LogTimeIntervals { get; } = Enum.GetValues(typeof(RollingInterval)).Cast<RollingInterval>().ToArray();
    public static SourceLevels[] SourceLevels { get; } = Enum.GetValues(typeof(SourceLevels)).Cast<SourceLevels>().ToArray();
    public static RevitEnricher[] AvailableRevitEnrichers { get; } =
    [
        RevitEnricher.RevitVersion,
        RevitEnricher.RevitBuild,
        RevitEnricher.RevitUserName,
        RevitEnricher.RevitLanguage,
        RevitEnricher.RevitDocumentTitle,
        RevitEnricher.RevitDocumentPathName,
        RevitEnricher.RevitDocumentModelPath
    ];

    [ObservableProperty] private LogLevel _logLevel;
    [ObservableProperty] private bool _enablePrettyJson;
    [ObservableProperty] private string _informationKeywords = string.Empty;
    [ObservableProperty] private string _warningKeywords = string.Empty;
    [ObservableProperty] private string _errorKeywords = string.Empty;
    [ObservableProperty] private string _criticalKeywords = string.Empty;
    [ObservableProperty] private bool _restartRequired;
    [ObservableProperty] private bool _isSaveLogEnabled;
    [ObservableProperty] private bool _useExternalFileOnly;
    [ObservableProperty] private SaveFormat _saveFormat;
    [ObservableProperty] private bool _includeStackTrace;
    [ObservableProperty] private SourceLevels _wpfTraceLevel;
    [ObservableProperty] private bool _includeWpfTrace;
    [ObservableProperty] private RollingInterval _timeInterval;
    [ObservableProperty] private int _stackTraceDepth;
    [ObservableProperty] private string _logFolder = string.Empty;
    [ObservableProperty] private bool _autoClean;

    public ObservableCollection<RevitEnricher> SelectedRevitEnrichers { get; } = [];

    private Snapshot _baseline;

    public LogSettingsViewModel(ISettingsService settingsService, ILoggingService loggingService)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
        _messenger = WeakReferenceMessenger.Default;

        SelectedRevitEnrichers.CollectionChanged += (_, _) => UpdateHasPendingChanges();

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

    partial void OnIsSaveLogEnabledChanged(bool value)
    {
        WeakReferenceMessenger.Default.Send(new IsSaveLogChangedMessage(value));
        UpdateHasPendingChanges();
    }
    partial void OnUseExternalFileOnlyChanged(bool value) => UpdateHasPendingChanges();
    partial void OnSaveFormatChanged(SaveFormat value) => UpdateHasPendingChanges();
    partial void OnIncludeStackTraceChanged(bool value) => UpdateHasPendingChanges();

    partial void OnWpfTraceLevelChanged(SourceLevels value)
    {
        _settingsService.LogConfig.TraceListener.WpfTraceLevel = value;
        PresentationTraceSources.DataBindingSource.Switch.Level = value;
    }

    partial void OnIncludeWpfTraceChanged(bool value) => UpdateHasPendingChanges();
    partial void OnTimeIntervalChanged(RollingInterval value) => UpdateHasPendingChanges();
    partial void OnStackTraceDepthChanged(int value) => UpdateHasPendingChanges();
    partial void OnLogFolderChanged(string value) => UpdateHasPendingChanges();
    partial void OnAutoCleanChanged(bool value) => UpdateHasPendingChanges();
    partial void OnEnablePrettyJsonChanged(bool value)
    {
        _loggingService.SetPrettyJson(value);
        UpdateHasPendingChanges();
    }
    partial void OnInformationKeywordsChanged(string value) => UpdateHasPendingChanges();
    partial void OnWarningKeywordsChanged(string value) => UpdateHasPendingChanges();
    partial void OnErrorKeywordsChanged(string value) => UpdateHasPendingChanges();
    partial void OnCriticalKeywordsChanged(string value) => UpdateHasPendingChanges();

    private void LoadFromConfig()
    {
        var config = _settingsService.LogConfig;

        LogLevel = config.TraceListener.LogLevel;
        EnablePrettyJson = config.Monitor.EnablePrettyJson;
        InformationKeywords = config.TraceListener.FilterKeywords.Information;
        WarningKeywords = config.TraceListener.FilterKeywords.Warning;
        ErrorKeywords = config.TraceListener.FilterKeywords.Error;
        CriticalKeywords = config.TraceListener.FilterKeywords.Critical;
        IsSaveLogEnabled = config.FileLogging.Enabled;
        UseExternalFileOnly = config.Monitor.UseExternalFileOnly;
        SaveFormat = config.FileLogging.Format;
        IncludeStackTrace = config.TraceListener.IncludeStackTrace;
        IncludeWpfTrace = config.TraceListener.IncludeWpfTrace;
        WpfTraceLevel = config.TraceListener.WpfTraceLevel;
        StackTraceDepth = config.TraceListener.StackTraceDepth;
        TimeInterval = config.FileLogging.RollingInterval;
        LogFolder = config.FileLogging.LogFolder;
        AutoClean = config.FileLogging.AutoClean;
        SelectedRevitEnrichers.Clear();
        foreach (var enricher in AvailableRevitEnrichers)
        {
            if (_settingsService.RevitEnrichers.HasFlag(enricher))
                SelectedRevitEnrichers.Add(enricher);
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

        config.TraceListener.LogLevel = LogLevel;
        config.Monitor.EnablePrettyJson = EnablePrettyJson;
        config.TraceListener.FilterKeywords.Information = InformationKeywords;
        config.TraceListener.FilterKeywords.Warning = WarningKeywords;
        config.TraceListener.FilterKeywords.Error = ErrorKeywords;
        config.TraceListener.FilterKeywords.Critical = CriticalKeywords;
        config.FileLogging.Enabled = IsSaveLogEnabled;
        config.Monitor.UseExternalFileOnly = UseExternalFileOnly;
        config.FileLogging.Format = SaveFormat;
        config.TraceListener.IncludeStackTrace = IncludeStackTrace;
        config.TraceListener.IncludeWpfTrace = IncludeWpfTrace;
        config.TraceListener.WpfTraceLevel = WpfTraceLevel;
        config.TraceListener.StackTraceDepth = StackTraceDepth;
        config.FileLogging.RollingInterval = TimeInterval;
        config.FileLogging.LogFolder = LogFolder;
        config.FileLogging.AutoClean = AutoClean;
        _settingsService.RevitEnrichers = SelectedRevitEnrichers.Aggregate(RevitEnricher.None, (current, enricher) => current | enricher);

        _settingsService.SaveSettings();
    }

    public void ApplyIfPendingChanges()
    {
        SaveToConfig();

        var target = ComputeLogTarget();
        SetBaselineFromCurrent();

        if (target != LogTargets.None)
            _messenger.Send(new LogSettingsAppliedMessage(target));
    }

    private LogTargets ComputeLogTarget()
    {
        var target = LogTargets.None;

        if (FileLoggingChanged())
            target |= LogTargets.File;

        if (MonitorChanged())
            target |= LogTargets.Monitor;

        if (TraceListenerChanged())
            target = LogTargets.All;

        return target;
    }

    private bool FileLoggingChanged()
    {
        var currentEnrichers = SelectedRevitEnrichers.Aggregate(RevitEnricher.None, (current, e) => current | e);
        return _baseline.IsSaveLogEnabled != IsSaveLogEnabled
            || _baseline.SaveFormat != SaveFormat
            || _baseline.TimeInterval != TimeInterval
            || _baseline.AutoClean != AutoClean
            || _baseline.RevitEnrichers != currentEnrichers
            || !string.Equals(_baseline.LogFolder, LogFolder, StringComparison.OrdinalIgnoreCase);
    }

    private bool MonitorChanged()
    {
        return _baseline.UseExternalFileOnly != UseExternalFileOnly;
    }

    private bool TraceListenerChanged()
    {
        return _baseline.IncludeStackTrace != IncludeStackTrace
            || _baseline.StackTraceDepth != StackTraceDepth
            || _baseline.IncludeWpfTrace != IncludeWpfTrace
            || _baseline.InformationKeywords != InformationKeywords
            || _baseline.WarningKeywords != WarningKeywords
            || _baseline.ErrorKeywords != ErrorKeywords
            || _baseline.CriticalKeywords != CriticalKeywords;
    }

    private void SetBaselineFromCurrent()
    {
        _baseline = new Snapshot(
            IsSaveLogEnabled,
            UseExternalFileOnly,
            SaveFormat,
            IncludeStackTrace,
            StackTraceDepth,
            IncludeWpfTrace,
            TimeInterval,
            LogFolder,
            AutoClean,
            InformationKeywords,
            WarningKeywords,
            ErrorKeywords,
            CriticalKeywords,
            SelectedRevitEnrichers.Aggregate(RevitEnricher.None, (current, e) => current | e)
        );

        RestartRequired = false;
    }

    private void UpdateHasPendingChanges()
    {
        RestartRequired = FileLoggingChanged() || MonitorChanged() || TraceListenerChanged();
    }

    private readonly record struct Snapshot(
        bool IsSaveLogEnabled,
        bool UseExternalFileOnly,
        SaveFormat SaveFormat,
        bool IncludeStackTrace,
        int StackTraceDepth,
        bool IncludeWpfTrace,
        RollingInterval TimeInterval,
        string LogFolder,
        bool AutoClean,
        string InformationKeywords,
        string WarningKeywords,
        string ErrorKeywords,
        string CriticalKeywords,
        RevitEnricher RevitEnrichers);

    [RelayCommand]
    private void BrowseFolder()
    {
        var selectedFolder = SettingsUtils.SelectFolder("Select Log Folder");
        if (!string.IsNullOrEmpty(selectedFolder))
        {
            LogFolder = selectedFolder;
        }
    }
}

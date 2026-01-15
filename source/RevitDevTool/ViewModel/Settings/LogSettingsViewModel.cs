using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using RevitDevTool.Logging;
using RevitDevTool.Logging.Enums;
using RevitDevTool.Settings;
using RevitDevTool.Utils;
using RevitDevTool.ViewModel.Messages;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

// ReSharper disable UnusedParameterInPartialMethod

namespace RevitDevTool.ViewModel.Settings;

public partial class LogSettingsViewModel : ObservableObject, IDataErrorInfo, IRecipient<ResetSettingsMessage>
{
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;
    private readonly IMessenger _messenger;

    public static int[] StackTraceDepths { get; } = [1, 2, 3, 4, 5]; // allowed maximum 5 levels
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
    [ObservableProperty] private bool _hasPendingChanges;
    [ObservableProperty] private bool _isSaveLogEnabled;
    [ObservableProperty] private bool _useExternalFileOnly;
    [ObservableProperty] private LogSaveFormat _saveFormat;
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
        _messenger.Register<ResetSettingsMessage>(this);
    }

    public string Error => string.Empty;

    public string this[string columnName] => columnName switch
    {
        nameof(InformationKeywords) => TraceUtils.ValidateKeywords(InformationKeywords) ?? string.Empty,
        nameof(WarningKeywords) => TraceUtils.ValidateKeywords(WarningKeywords) ?? string.Empty,
        nameof(ErrorKeywords) => TraceUtils.ValidateKeywords(ErrorKeywords) ?? string.Empty,
        nameof(CriticalKeywords) => TraceUtils.ValidateKeywords(CriticalKeywords) ?? string.Empty,
        _ => string.Empty
    };

    partial void OnLogLevelChanged(LogLevel value)
    {
        _settingsService.LogConfig.LogLevel = value;
        _loggingService.SetMinimumLevel(value);
    }

    partial void OnIsSaveLogEnabledChanged(bool value)
    {
        WeakReferenceMessenger.Default.Send(new IsSaveLogChangedMessage(value));
        UpdateHasPendingChanges();
    }
    partial void OnUseExternalFileOnlyChanged(bool value) => UpdateHasPendingChanges();
    partial void OnSaveFormatChanged(LogSaveFormat value) => UpdateHasPendingChanges();
    partial void OnIncludeStackTraceChanged(bool value) => UpdateHasPendingChanges();

    partial void OnWpfTraceLevelChanged(SourceLevels value)
    {
        _settingsService.LogConfig.WpfTraceLevel = value;
        PresentationTraceSources.DataBindingSource.Switch.Level = value;
    }

    partial void OnIncludeWpfTraceChanged(bool value) => UpdateHasPendingChanges();
    partial void OnTimeIntervalChanged(RollingInterval value) => UpdateHasPendingChanges();
    partial void OnStackTraceDepthChanged(int value) => UpdateHasPendingChanges();
    partial void OnLogFolderChanged(string value) => UpdateHasPendingChanges();
    partial void OnAutoCleanChanged(bool value) => UpdateHasPendingChanges();
    partial void OnEnablePrettyJsonChanged(bool value) => UpdateHasPendingChanges();
    partial void OnInformationKeywordsChanged(string value) => UpdateHasPendingChanges();
    partial void OnWarningKeywordsChanged(string value) => UpdateHasPendingChanges();
    partial void OnErrorKeywordsChanged(string value) => UpdateHasPendingChanges();
    partial void OnCriticalKeywordsChanged(string value) => UpdateHasPendingChanges();

    private void LoadFromConfig()
    {
        var config = _settingsService.LogConfig;

        LogLevel = config.LogLevel;
        EnablePrettyJson = config.EnablePrettyJson;
        InformationKeywords = config.FilterKeywords.Information;
        WarningKeywords = config.FilterKeywords.Warning;
        ErrorKeywords = config.FilterKeywords.Error;
        CriticalKeywords = config.FilterKeywords.Critical;
        IsSaveLogEnabled = config.IsSaveLogEnabled;
        UseExternalFileOnly = config.UseExternalFileOnly;
        SaveFormat = config.SaveFormat;
        IncludeStackTrace = config.IncludeStackTrace;
        IncludeWpfTrace = config.IncludeWpfTrace;
        WpfTraceLevel = config.WpfTraceLevel;
        StackTraceDepth = config.StackTraceDepth;
        TimeInterval = config.TimeInterval;
        LogFolder = config.LogFolder;
        AutoClean = config.AutoClean;
        SelectedRevitEnrichers.Clear();
        foreach (var enricher in AvailableRevitEnrichers)
        {
            if (config.RevitEnrichers.HasFlag(enricher))
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

        config.LogLevel = LogLevel;
        config.EnablePrettyJson = EnablePrettyJson;
        config.FilterKeywords.Information = InformationKeywords;
        config.FilterKeywords.Warning = WarningKeywords;
        config.FilterKeywords.Error = ErrorKeywords;
        config.FilterKeywords.Critical = CriticalKeywords;
        config.IsSaveLogEnabled = IsSaveLogEnabled;
        config.UseExternalFileOnly = UseExternalFileOnly;
        config.SaveFormat = SaveFormat;
        config.IncludeStackTrace = IncludeStackTrace;
        config.IncludeWpfTrace = IncludeWpfTrace;
        config.WpfTraceLevel = WpfTraceLevel;
        config.StackTraceDepth = StackTraceDepth;
        config.TimeInterval = TimeInterval;
        config.LogFolder = LogFolder;
        config.AutoClean = AutoClean;
        config.RevitEnrichers = SelectedRevitEnrichers
            .Aggregate(RevitEnricher.None, (current, enricher) => current | enricher);

        _settingsService.SaveSettings();
    }

    /// <summary>
    /// Apply pending changes (save settings + notify restart) only once when closing Settings.
    /// This avoids restarting logging on every property change, but still guarantees the new
    /// settings are applied when user navigates back.
    /// </summary>
    public void ApplyIfPendingChanges()
    {
        if (!HasPendingChanges) return;
        SaveToConfig();
        SetBaselineFromCurrent();
        _messenger.Send(new LogSettingsAppliedMessage());
    }

    private void SetBaselineFromCurrent()
    {
        _baseline = new Snapshot(
            IsSaveLogEnabled,
            UseExternalFileOnly,
            SaveFormat,
            IncludeStackTrace,
            IncludeWpfTrace,
            TimeInterval,
            LogFolder,
            EnablePrettyJson,
            SelectedRevitEnrichers.Aggregate(RevitEnricher.None, (current, e) => current | e)
        );

        HasPendingChanges = false;
    }

    private void UpdateHasPendingChanges()
    {
        var currentEnrichers = SelectedRevitEnrichers.Aggregate(RevitEnricher.None, (current, e) => current | e);

        HasPendingChanges =
            _baseline.IsSaveLogEnabled != IsSaveLogEnabled
            || _baseline.UseExternalFileOnly != UseExternalFileOnly
            || _baseline.SaveFormat != SaveFormat
            || _baseline.IncludeStackTrace != IncludeStackTrace
            || _baseline.IncludeWpfTrace != IncludeWpfTrace
            || _baseline.TimeInterval != TimeInterval
            || _baseline.EnablePrettyJson != EnablePrettyJson
            || _baseline.RevitEnrichers != currentEnrichers
            || !string.Equals(_baseline.LogFolder, LogFolder, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct Snapshot(
        bool IsSaveLogEnabled,
        bool UseExternalFileOnly,
        LogSaveFormat SaveFormat,
        bool IncludeStackTrace,
        bool IncludeWpfTrace,
        RollingInterval TimeInterval,
        string LogFolder,
        bool EnablePrettyJson,
        RevitEnricher RevitEnrichers);

    [RelayCommand]
    private void BrowseFolder()
    {
#if NET8_0_OR_GREATER
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select log folder",
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            LogFolder = dialog.FolderName;
        }
#else
        using var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
        dialog.Title = "Select log folder";
        dialog.IsFolderPicker = true;
        dialog.Multiselect = false;
        if (dialog.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
        {
            LogFolder = dialog.FileName;
        }
#endif
    }
}

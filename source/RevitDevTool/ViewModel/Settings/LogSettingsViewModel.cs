using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using RevitDevTool.Messages;
using RevitDevTool.Models.Config;
using RevitDevTool.Services;
using RevitDevTool.Utils;
using System.Diagnostics;

// ReSharper disable UnusedParameterInPartialMethod

namespace RevitDevTool.ViewModel.Settings;

public partial class LogSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IMessenger _messenger;

    public static int[] StackTraceDepths { get; } = [1, 2, 3, 4, 5]; // allowed maximum 5 levels
    public static SourceLevels[] SourceLevels { get; } = Enum.GetValues(typeof(SourceLevels)).Cast<SourceLevels>().ToArray();

    [ObservableProperty] private LogLevel _logLevel;
    [ObservableProperty] private bool _hasPendingChanges;
    [ObservableProperty] private bool _isSaveLogEnabled;
    [ObservableProperty] private bool _useExternalFileOnly;
    [ObservableProperty] private LogSaveFormat _saveFormat;
    [ObservableProperty] private bool _includeStackTrace;
    [ObservableProperty] private SourceLevels _wpfTraceLevel;
    [ObservableProperty] private bool _includeWpfTrace;
    [ObservableProperty] private RollingInterval _timeInterval;
    [ObservableProperty] private int _stackTraceDepth;
    [ObservableProperty] private string _filePath = string.Empty;

    private Snapshot _baseline;

    public LogSettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _messenger = WeakReferenceMessenger.Default;

        LoadFromConfig();
        SetBaselineFromCurrent();
    }

    partial void OnLogLevelChanged(LogLevel value) => UpdateHasPendingChanges();
    partial void OnIsSaveLogEnabledChanged(bool value) => UpdateHasPendingChanges();
    partial void OnUseExternalFileOnlyChanged(bool value) => UpdateHasPendingChanges();
    partial void OnSaveFormatChanged(LogSaveFormat value)
    {
        CorrectFilePath();
        UpdateHasPendingChanges();
    }
    partial void OnIncludeStackTraceChanged(bool value) => UpdateHasPendingChanges();
    partial void OnWpfTraceLevelChanged(SourceLevels value) => UpdateHasPendingChanges();
    partial void OnIncludeWpfTraceChanged(bool value) => UpdateHasPendingChanges();
    partial void OnTimeIntervalChanged(RollingInterval value) => UpdateHasPendingChanges();
    partial void OnStackTraceDepthChanged(int value) => UpdateHasPendingChanges();
    partial void OnFilePathChanged(string value) => UpdateHasPendingChanges();

    private void LoadFromConfig()
    {
        var config = _settingsService.LogConfig;
        LogLevel = config.LogLevel;
        IsSaveLogEnabled = config.IsSaveLogEnabled;
        UseExternalFileOnly = config.UseExternalFileOnly;
        SaveFormat = config.SaveFormat;
        IncludeStackTrace = config.IncludeStackTrace;
        IncludeWpfTrace = config.IncludeWpfTrace;
        WpfTraceLevel = config.WpfTraceLevel;
        StackTraceDepth = config.StackTraceDepth;
        TimeInterval = config.TimeInterval;
        FilePath = config.FilePath;
        CorrectFilePath();
    }

    private void SaveToConfig()
    {
        var config = _settingsService.LogConfig;
        config.LogLevel = LogLevel;
        config.IsSaveLogEnabled = IsSaveLogEnabled;
        config.UseExternalFileOnly = UseExternalFileOnly;
        config.SaveFormat = SaveFormat;
        config.IncludeStackTrace = IncludeStackTrace;
        config.IncludeWpfTrace = IncludeWpfTrace;
        config.WpfTraceLevel = WpfTraceLevel;
        config.StackTraceDepth = StackTraceDepth;
        config.TimeInterval = TimeInterval;
        CorrectFilePath();
        config.FilePath = FilePath;
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
            LogLevel,
            IsSaveLogEnabled,
            UseExternalFileOnly,
            SaveFormat,
            IncludeStackTrace,
            WpfTraceLevel,
            IncludeWpfTrace,
            TimeInterval,
            StackTraceDepth,
            FilePath
        );

        HasPendingChanges = false;
    }

    private void UpdateHasPendingChanges()
    {
        HasPendingChanges =
            _baseline.LogLevel != LogLevel
            || _baseline.IsSaveLogEnabled != IsSaveLogEnabled
            || _baseline.UseExternalFileOnly != UseExternalFileOnly
            || _baseline.SaveFormat != SaveFormat
            || _baseline.IncludeStackTrace != IncludeStackTrace
            || _baseline.WpfTraceLevel != WpfTraceLevel
            || _baseline.IncludeWpfTrace != IncludeWpfTrace
            || _baseline.TimeInterval != TimeInterval
            || _baseline.StackTraceDepth != StackTraceDepth
            || !string.Equals(_baseline.FilePath, FilePath, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct Snapshot(
        LogLevel LogLevel,
        bool IsSaveLogEnabled,
        bool UseExternalFileOnly,
        LogSaveFormat SaveFormat,
        bool IncludeStackTrace,
        SourceLevels WpfTraceLevel,
        bool IncludeWpfTrace,
        RollingInterval TimeInterval,
        int StackTraceDepth,
        string FilePath);

    private void CorrectFilePath()
    {
        var isReadOnly = SettingsUtils.CheckWriteAccess(FilePath);
        var isNotValidPath = SettingsUtils.CheckValidPath(FilePath);
        if (isNotValidPath || isReadOnly)
        {
            FilePath = SettingsUtils.GetDefaultLogPath(FileExtension);
        }
    }

    private string FileExtension => SaveFormat switch
    {
        LogSaveFormat.Sqlite => "db",
        LogSaveFormat.Json => "json",
        LogSaveFormat.Clef => "clef",
        _ => "log"
    };

    [RelayCommand]
    private void BrowseFile()
    {
        var filter = SaveFormat switch
        {
            LogSaveFormat.Sqlite => "SQLite Database (*.db)|*.db",
            LogSaveFormat.Json => "JSON Files (*.json)|*.json",
            LogSaveFormat.Clef => "CLEF Files (*.clef)|*.clef",
            _ => "All Files (*.*)|*.*"
        };

        var extension = FileExtension;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            DefaultExt = extension,
            FileName = "log"
        };

        if (dialog.ShowDialog() == true)
        {
            FilePath = dialog.FileName;
        }
    }
}



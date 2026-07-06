using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.Logging;
using ZLogger;
using CommunityToolkit.Mvvm.Messaging;
using DevTools.Presentation.ViewModels.Messages;
using DevTools.Presentation.ViewModels.Settings;
using DevTools.Presentation.Views;
using DevTools.Settings;
namespace DevTools.Presentation.ViewModels;

public partial class MainViewModel : ObservableRecipient,
    IRecipient<IsSaveLogChangedMessage>,
    IRecipient<IsMemoryEnableChangedMessage>
{
    private readonly LogSettingsViewModel _logSettingsViewModel;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<MainViewModel> _logger;
    private bool _isInterlockingVisibility;

    public LogViewModel LogViewModel { get; }
    public FrameworkElement ExecutionView { get; }
    public FrameworkElement McpRegistryView { get; }
    public FrameworkElement MemoryView { get; }
    public int ProcessId { get; } = Environment.ProcessId;
    public bool ShowLogMonitorOnly => !IsExecutionVisible && !IsMcpVisible;

    [ObservableProperty]
    public partial bool IsSettingsVisible { get; set; }

    [ObservableProperty]
    public partial bool IsExecutionVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsMcpVisible { get; set; }

    [ObservableProperty]
    public partial bool IsMemoryEnabled { get; set; }

    partial void OnIsSettingsVisibleChanged(bool value)
    {
        if (!value) _logSettingsViewModel.ApplyIfPendingChanges();
    }

    partial void OnIsExecutionVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLogMonitorOnly));
        if (_isInterlockingVisibility || !value) return;
        try { _isInterlockingVisibility = true; IsMcpVisible = false; }
        finally { _isInterlockingVisibility = false; }
    }

    partial void OnIsMcpVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLogMonitorOnly));
        if (_isInterlockingVisibility || !value) return;
        try { _isInterlockingVisibility = true; IsExecutionVisible = false; }
        finally { _isInterlockingVisibility = false; }
    }

    [RelayCommand(CanExecute = nameof(CanOpenLogFolder))]
    private void OpenLogFolder()
    {
        var logFolder = _settingsService.LogConfig.FileLogging.LogFolder;
        try { Process.Start("explorer.exe", logFolder); }
        catch (Exception ex) { _logger.ZLogError($"Failed to open log folder: {ex.Message}"); }
    }

    private bool CanOpenLogFolder() => _settingsService.LogConfig.FileLogging.Enabled;

    public MainViewModel(
        LogViewModel logViewModel,
        ExecutionView executionView,
        McpRegistryView mcpRegistryView,
        MemoryView memoryView,
        LogSettingsViewModel logSettingsViewModel,
        ISettingsService settingsService,
        ILogger<MainViewModel> logger)
    {
        LogViewModel = logViewModel;
        ExecutionView = executionView;
        McpRegistryView = mcpRegistryView;
        MemoryView = memoryView;
        _logSettingsViewModel = logSettingsViewModel;
        _settingsService = settingsService;
        _logger = logger;
        IsMemoryEnabled = settingsService.GeneralConfig.IsMemoryEnabled;
        IsActive = true;
        ApplyMemoryMonitorState(IsMemoryEnabled);
    }

    public void Receive(IsSaveLogChangedMessage message)
    {
        OpenLogFolderCommand.NotifyCanExecuteChanged();
    }

    public void Receive(IsMemoryEnableChangedMessage message)
    {
        IsMemoryEnabled = message.IsEnabled;
        ApplyMemoryMonitorState(message.IsEnabled);
    }

    private void ApplyMemoryMonitorState(bool enabled)
    {
        if (MemoryView.DataContext is not MemoryViewModel vm) return;
        if (enabled) vm.Start();
        else vm.Stop();
    }
}

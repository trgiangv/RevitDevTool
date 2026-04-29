using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using DevTools.Presentation.Interfaces;
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
    private bool _isInterlockingVisibility;

    public LogViewModel LogViewModel { get; }
    public FrameworkElement ExecutionView { get; }
    public FrameworkElement McpRegistryView { get; }
    public FrameworkElement MemoryView { get; }
    public int ProcessId { get; } = Environment.ProcessId;
    public bool ShowLogMonitorOnly => !IsExecutionVisible && !IsMcpVisible;

    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private bool _isSettingsVisible;
    [ObservableProperty] private bool _isExecutionVisible = true;
    [ObservableProperty] private bool _isMcpVisible;
    [ObservableProperty] private bool _isSaveLogEnabled;
    [ObservableProperty] private bool _isMemoryEnabled;

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
        catch (Exception ex) { Trace.TraceError($"Failed to open log folder: {ex.Message}"); }
    }

    private bool CanOpenLogFolder() => IsSaveLogEnabled;

    public MainViewModel(
        LogViewModel logViewModel,
        ExecutionView executionView,
        McpRegistryView mcpRegistryView,
        MemoryView memoryView,
        LogSettingsViewModel logSettingsViewModel,
        ISettingsService settingsService)
    {
        LogViewModel = logViewModel;
        ExecutionView = executionView;
        McpRegistryView = mcpRegistryView;
        MemoryView = memoryView;
        _logSettingsViewModel = logSettingsViewModel;
        _settingsService = settingsService;
        IsSaveLogEnabled = settingsService.LogConfig.FileLogging.Enabled;
        IsMemoryEnabled = settingsService.GeneralConfig.IsMemoryEnabled;
        IsActive = true;
        ApplyMemoryMonitorState(IsMemoryEnabled);
    }

    public void Receive(IsSaveLogChangedMessage message)
    {
        IsSaveLogEnabled = message.IsEnabled;
        OpenLogFolderCommand.NotifyCanExecuteChanged();
    }

    public void Receive(IsMemoryEnableChangedMessage message)
    {
        IsMemoryEnabled = message.IsEnabled;
        ApplyMemoryMonitorState(message.IsEnabled);
    }

    private void ApplyMemoryMonitorState(bool enabled)
    {
        if (MemoryView.DataContext is MemoryViewModel vm)
        {
            if (enabled) vm.Start();
            else vm.Stop();
        }
    }
}

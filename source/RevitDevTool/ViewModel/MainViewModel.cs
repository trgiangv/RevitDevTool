using CommunityToolkit.Mvvm.Messaging;
using RevitDevTool.Settings;
using RevitDevTool.View;
using RevitDevTool.ViewModel.Messages;
using RevitDevTool.ViewModel.Settings;
using System.Diagnostics;

namespace RevitDevTool.ViewModel;

public partial class MainViewModel : ObservableRecipient, IRecipient<IsSaveLogChangedMessage>, IRecipient<IsMemoryEnableChangedMessage>
{
    private readonly LogSettingsViewModel _logSettingsViewModel;
    private readonly ISettingsService _settingsService;
    private bool _isInterlockingVisibility;

    public LogViewModel LogViewModel { get; }
    public ExecutionView ExecutionView { get; }
    public McpRegistryView McpRegistryView { get; }
    public MemoryView MemoryView { get; }
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
        if (!value)
        {
            _logSettingsViewModel.ApplyIfPendingChanges();
        }
    }

    partial void OnIsExecutionVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLogMonitorOnly));
        if (_isInterlockingVisibility || !value) return;

        try
        {
            _isInterlockingVisibility = true;
            IsMcpVisible = false;
        }
        finally
        {
            _isInterlockingVisibility = false;
        }

    }

    partial void OnIsMcpVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLogMonitorOnly));
        if (_isInterlockingVisibility || !value) return;

        try
        {
            _isInterlockingVisibility = true;
            IsExecutionVisible = false;
        }
        finally
        {
            _isInterlockingVisibility = false;
        }

    }

    [RelayCommand(CanExecute = nameof(CanOpenLogFolder))]
    private void OpenLogFolder()
    {
        var logFolder = _settingsService.LogConfig.FileLogging.LogFolder;
        try
        {
            Process.Start("explorer.exe", logFolder);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to open log folder: {ex.Message}");
        }
    }

    private bool CanOpenLogFolder()
    {
        return IsSaveLogEnabled;
    }

    public MainViewModel(
        LogViewModel logViewModel,
        ExecutionView addinLoadView,
        McpRegistryView mcpRegistryView,
        MemoryView memoryView,
        LogSettingsViewModel logSettingsViewModel,
        ISettingsService settingsService)
    {
        LogViewModel = logViewModel;
        ExecutionView = addinLoadView;
        McpRegistryView = mcpRegistryView;
        MemoryView = memoryView;
        IsSaveLogEnabled = settingsService.LogConfig.FileLogging.Enabled;
        IsMemoryEnabled = settingsService.GeneralConfig.IsMemoryEnabled;
        IsActive = true;
        _logSettingsViewModel = logSettingsViewModel;
        _settingsService = settingsService;
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
        var memoryViewModel = MemoryView.DataContext as MemoryViewModel;
        if (enabled)
            memoryViewModel?.Start();
        else
            memoryViewModel?.Stop();
    }
}

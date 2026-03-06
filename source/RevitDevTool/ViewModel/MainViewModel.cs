using CommunityToolkit.Mvvm.Messaging;
using RevitDevTool.Settings;
using RevitDevTool.Utils;
using RevitDevTool.View;
using RevitDevTool.ViewModel.Messages;
using RevitDevTool.ViewModel.Settings;
using System.Diagnostics;

namespace RevitDevTool.ViewModel;

/// <summary>
/// ViewModel for the main MainPage that handles navigation
/// </summary>
public partial class MainViewModel : ObservableObject, IRecipient<IsSaveLogChangedMessage>
{
    private readonly LogSettingsViewModel _logSettingsViewModel;
    private readonly ISettingsService _settingsService;
    private bool _isInterlockingVisibility;

    public LogViewModel LogViewModel { get; }
    public ExecutionView ExecutionView { get; }
    public McpRegistryView McpRegistryView { get; }
    public MemoryView MemoryView { get; }
    public int ProcessId { get; } = SettingsUtils.CurrentProcessId;
    public bool ShowLogMonitorOnly => !IsExecutionVisible && !IsMcpVisible;
    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private bool _isSettingsVisible;
    [ObservableProperty] private bool _isExecutionVisible = true;
    [ObservableProperty] private bool _isMcpVisible;
    [ObservableProperty] private bool _isSaveLogEnabled;

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
        var logFolder = _settingsService.LogConfig.LogFolder;
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
        IsSaveLogEnabled = settingsService.LogConfig.IsSaveLogEnabled;
        _logSettingsViewModel = logSettingsViewModel;
        _settingsService = settingsService;
        WeakReferenceMessenger.Default.Register(this);
    }

    public void Receive(IsSaveLogChangedMessage message)
    {
        IsSaveLogEnabled = message.Value;
        OpenLogFolderCommand.NotifyCanExecuteChanged();
    }
}

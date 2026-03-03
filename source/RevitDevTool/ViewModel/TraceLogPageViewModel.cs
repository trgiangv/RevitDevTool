using CommunityToolkit.Mvvm.Messaging;
using RevitDevTool.Settings;
using RevitDevTool.Utils;
using RevitDevTool.View;
using RevitDevTool.ViewModel.Messages;
using RevitDevTool.ViewModel.Settings;
using System.Diagnostics;

namespace RevitDevTool.ViewModel;

/// <summary>
/// ViewModel for the main TraceLogPage that handles navigation
/// </summary>
public partial class TraceLogPageViewModel : ObservableObject, IRecipient<IsSaveLogChangedMessage>
{
    private readonly LogSettingsViewModel _logSettingsViewModel;
    private readonly ISettingsService _settingsService;

    public TraceLogViewModel TraceLogViewModel { get; }
    public ExecutionView ExecutionView { get; }
    public MemoryView MemoryView { get; }
    public int ProcessId { get; } = SettingsUtils.CurrentProcessId;
    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private bool _isSettingsVisible;
    [ObservableProperty] private bool _isAddinLoadVisible = true;
    [ObservableProperty] private bool _isSaveLogEnabled;

    partial void OnIsSettingsVisibleChanged(bool value)
    {
        if (!value)
        {
            _logSettingsViewModel.ApplyIfPendingChanges();
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

    public TraceLogPageViewModel(
        TraceLogViewModel traceLogViewModel,
        ExecutionView addinLoadView,
        MemoryView memoryView,
        LogSettingsViewModel logSettingsViewModel,
        ISettingsService settingsService)
    {
        TraceLogViewModel = traceLogViewModel;
        ExecutionView = addinLoadView;
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

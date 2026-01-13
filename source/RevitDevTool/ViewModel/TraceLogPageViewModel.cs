using CommunityToolkit.Mvvm.Messaging;
using RevitDevTool.Messages;
using RevitDevTool.Services;
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
    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private bool _isSettingsVisible;
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
            Process.Start(new ProcessStartInfo
            {
                FileName = logFolder,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore
        }
    }

    private bool CanOpenLogFolder()
    {
        return IsSaveLogEnabled;
    }

    public TraceLogPageViewModel(
        TraceLogViewModel traceLogViewModel,
        LogSettingsViewModel logSettingsViewModel,
        ISettingsService settingsService)
    {
        TraceLogViewModel = traceLogViewModel;
        _logSettingsViewModel = logSettingsViewModel;
        _settingsService = settingsService;
        IsSaveLogEnabled = settingsService.LogConfig.IsSaveLogEnabled;
        WeakReferenceMessenger.Default.Register(this);
    }

    public void Receive(IsSaveLogChangedMessage message)
    {
        IsSaveLogEnabled = message.Value;
        OpenLogFolderCommand.NotifyCanExecuteChanged();
    }
}

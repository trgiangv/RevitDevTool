using RevitDevTool.ViewModel.Settings;
namespace RevitDevTool.ViewModel;

/// <summary>
/// ViewModel for the main TraceLogPage that handles navigation
/// </summary>
public partial class TraceLogPageViewModel(TraceLogViewModel traceLogViewModel, LogSettingsViewModel logSettingsViewModel) : ObservableObject
{
    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private bool _isSettingsVisible;

    public TraceLogViewModel TraceLogViewModel { get; } = traceLogViewModel;

    [RelayCommand]
    private void ToggleSettings()
    {
        if (IsSettingsVisible)
        {
            logSettingsViewModel.ApplyIfPendingChanges();
        }

        IsSettingsVisible = !IsSettingsVisible;
    }

    [RelayCommand]
    private void NavigateBack()
    {
        logSettingsViewModel.ApplyIfPendingChanges();
        IsSettingsVisible = false;
    }
}


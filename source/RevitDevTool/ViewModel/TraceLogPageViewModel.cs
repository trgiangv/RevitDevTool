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

    partial void OnIsSettingsVisibleChanged(bool value)
    {
        if (!value)
        {
            logSettingsViewModel.ApplyIfPendingChanges();
        }
    }
}


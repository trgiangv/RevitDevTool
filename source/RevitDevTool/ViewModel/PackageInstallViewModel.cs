using RevitDevTool.Utils;

namespace RevitDevTool.ViewModel;

public partial class PackageInstallViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusMessage = "Initializing...";

    public Action<bool>? CloseAction { get; set; }

    public void UpdateProgress(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        
        DispatcherHelper.RunOnMainThread(() =>
        {
            StatusMessage = message;
        });
    }

    public void OnInstallationComplete(bool success)
    {
        DispatcherHelper.RunOnMainThread(() =>
        {
            StatusMessage = success ? "Installation complete!" : "Installation failed.";
            CloseAction?.Invoke(success);
        });
    }
}

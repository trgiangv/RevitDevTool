using DevTools.Presentation.ViewModels.Settings;

namespace DevTools.Presentation.Views.Settings;

public partial class McpSettingView
{
    public McpSettingView()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as McpSettingViewModel)?.Activate();
    }
}

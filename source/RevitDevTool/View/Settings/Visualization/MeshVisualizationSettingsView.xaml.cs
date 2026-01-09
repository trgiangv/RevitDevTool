using RevitDevTool.Theme;
using RevitDevTool.ViewModel.Settings.Visualization;
namespace RevitDevTool.View.Settings.Visualization;

public sealed partial class MeshVisualizationSettingsView
{
    public MeshVisualizationSettingsView(IThemeWatcherService themeWatcherService, MeshVisualizationViewModel viewModel)
    {
        themeWatcherService.Watch(this);
        DataContext = viewModel;
        InitializeComponent();
    }
}
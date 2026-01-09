using RevitDevTool.Theme;
using RevitDevTool.ViewModel.Settings.Visualization;
namespace RevitDevTool.View.Settings.Visualization;

public sealed partial class SolidVisualizationSettingsView
{
    public SolidVisualizationSettingsView(IThemeWatcherService themeWatcherService, SolidVisualizationViewModel viewModel)
    {
        themeWatcherService.Watch(this);
        DataContext = viewModel;
        InitializeComponent();
    }
}
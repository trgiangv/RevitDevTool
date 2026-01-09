using RevitDevTool.Theme;
using RevitDevTool.ViewModel.Settings.Visualization;
namespace RevitDevTool.View.Settings.Visualization;

public sealed partial class BoundingBoxVisualizationSettingsView
{
    public BoundingBoxVisualizationSettingsView(IThemeWatcherService themeWatcherService, BoundingBoxVisualizationViewModel viewModel)
    {
        themeWatcherService.Watch(this);
        DataContext = viewModel;
        InitializeComponent();
    }
}
using RevitDevTool.Theme;
using RevitDevTool.ViewModel.Settings.Visualization;
namespace RevitDevTool.View.Settings.Visualization;

public sealed partial class FaceVisualizationSettingsView
{
    public FaceVisualizationSettingsView(IThemeWatcherService themeWatcherService, FaceVisualizationViewModel viewModel)
    {
        themeWatcherService.Watch(this);
        DataContext = viewModel;
        InitializeComponent();
    }
}
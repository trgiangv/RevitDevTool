using RevitDevTool.Theme;
using RevitDevTool.ViewModel.Settings.Visualization;
namespace RevitDevTool.View.Settings.Visualization;

public sealed partial class XyzVisualizationSettingsView
{
    public XyzVisualizationSettingsView(IThemeWatcherService themeWatcherService, XyzVisualizationViewModel viewModel)
    {
        themeWatcherService.Watch(this);
        DataContext = viewModel;
        InitializeComponent();
    }
}
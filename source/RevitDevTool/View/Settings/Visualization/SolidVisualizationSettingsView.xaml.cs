using RevitDevTool.ViewModel.Settings.Visualization;

namespace RevitDevTool.View.Settings.Visualization;

public sealed partial class SolidVisualizationSettingsView
{
    public SolidVisualizationSettingsView(SolidVisualizationViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}

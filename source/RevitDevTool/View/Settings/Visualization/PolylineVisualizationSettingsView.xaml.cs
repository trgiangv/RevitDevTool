using RevitDevTool.ViewModel.Settings.Visualization;

namespace RevitDevTool.View.Settings.Visualization;

public sealed partial class PolylineVisualizationSettingsView
{
    public PolylineVisualizationSettingsView(PolylineVisualizationViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}

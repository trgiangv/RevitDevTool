using RevitDevTool.ViewModel.Settings.Visualization;

namespace RevitDevTool.View.Settings.Visualization;

public sealed partial class BoundingBoxVisualizationSettingsView
{
    public BoundingBoxVisualizationSettingsView(BoundingBoxVisualizationViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}

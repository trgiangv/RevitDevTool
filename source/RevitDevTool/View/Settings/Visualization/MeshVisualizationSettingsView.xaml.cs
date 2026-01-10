using RevitDevTool.ViewModel.Settings.Visualization;

namespace RevitDevTool.View.Settings.Visualization;

public sealed partial class MeshVisualizationSettingsView
{
    public MeshVisualizationSettingsView(MeshVisualizationViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}

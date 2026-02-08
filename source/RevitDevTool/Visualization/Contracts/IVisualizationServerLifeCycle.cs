using RevitDevTool.ViewModel.Settings.Visualization;
namespace RevitDevTool.Visualization.Contracts;

public interface IVisualizationServerLifeCycle
{
    void Register(IVisualizationViewModel visualizationViewModel);
    void Unregister();
    void ClearGeometry();
    int GeometryCount { get; }
}
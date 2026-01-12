using RevitDevTool.ViewModel.Contracts;

namespace RevitDevTool.Visualization.Contracts;

public interface IVisualizationServerLifeCycle
{
    public void Register(IVisualizationViewModel visualizationViewModel);
    public void Unregister();
    public void ClearGeometry();
}
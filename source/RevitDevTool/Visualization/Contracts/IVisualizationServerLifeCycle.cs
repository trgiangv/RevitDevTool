namespace RevitDevTool.Visualization.Contracts ;

public interface IVisualizationServerLifeCycle
{
    public void Register();
    public void Unregister();
    public void ClearGeometry();
}
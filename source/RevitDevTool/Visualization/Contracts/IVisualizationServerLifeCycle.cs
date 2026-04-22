namespace RevitDevTool.Visualization.Contracts;

public interface IVisualizationServerLifeCycle
{
    void Register();
    void Unregister();
    void ClearGeometry();
    int GeometryCount { get; }
}
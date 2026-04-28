using DevTools.Views.Interfaces;
using RevitDevTool.Controllers;

namespace RevitDevTool.Bridges;

public sealed class RevitVisualizationBridge : IVisualizationBridge
{
    public void Start() => VisualizationController.Start();
    public void Stop() => VisualizationController.Stop();
    public void Clear() => VisualizationController.Clear();
    public void Refresh() => VisualizationController.Refresh();
}

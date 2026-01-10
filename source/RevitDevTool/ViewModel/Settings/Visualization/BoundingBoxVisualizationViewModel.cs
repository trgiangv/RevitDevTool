using RevitDevTool.Controllers;
using RevitDevTool.Services;
using RevitDevTool.ViewModel.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings.Visualization;

public sealed partial class BoundingBoxVisualizationViewModel : ObservableObject, IVisualizationViewModel
{
    private readonly ISettingsService _settingsService;
    
    public BoundingBoxVisualizationViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        _transparency = _settingsService.VisualizationConfig.BoundingBoxSettings.Transparency;
        _scale = _settingsService.VisualizationConfig.BoundingBoxSettings.Scale;
        _surfaceColor = _settingsService.VisualizationConfig.BoundingBoxSettings.SurfaceColor;
        _edgeColor = _settingsService.VisualizationConfig.BoundingBoxSettings.EdgeColor;
        _axisColor = _settingsService.VisualizationConfig.BoundingBoxSettings.AxisColor;
        _showSurface = _settingsService.VisualizationConfig.BoundingBoxSettings.ShowSurface;
        _showEdge = _settingsService.VisualizationConfig.BoundingBoxSettings.ShowEdge;
        _showAxis = _settingsService.VisualizationConfig.BoundingBoxSettings.ShowAxis;
    }
    
    [ObservableProperty] private double _transparency;
    [ObservableProperty] private double _scale;
    
    [ObservableProperty] private System.Windows.Media.Color _surfaceColor;
    [ObservableProperty] private System.Windows.Media.Color _edgeColor;
    [ObservableProperty] private System.Windows.Media.Color _axisColor;
    
    [ObservableProperty] private bool _showSurface;
    [ObservableProperty] private bool _showEdge;
    [ObservableProperty] private bool _showAxis;
    
    public void Initialize()
    {
        UpdateShowSurface(ShowSurface);
        UpdateShowEdge(ShowEdge);
        UpdateShowAxis(ShowAxis);
        
        UpdateSurfaceColor(SurfaceColor);
        UpdateEdgeColor(EdgeColor);
        UpdateAxisColor(AxisColor);
        
        UpdateTransparency(Transparency);
        UpdateScale(Scale);
    }

    public void Refresh()
    {
        Transparency = _settingsService.VisualizationConfig.BoundingBoxSettings.Transparency;
        Scale = _settingsService.VisualizationConfig.BoundingBoxSettings.Scale;
        SurfaceColor = _settingsService.VisualizationConfig.BoundingBoxSettings.SurfaceColor;
        EdgeColor = _settingsService.VisualizationConfig.BoundingBoxSettings.EdgeColor;
        AxisColor = _settingsService.VisualizationConfig.BoundingBoxSettings.AxisColor;
        ShowSurface = _settingsService.VisualizationConfig.BoundingBoxSettings.ShowSurface;
        ShowEdge = _settingsService.VisualizationConfig.BoundingBoxSettings.ShowEdge;
        ShowAxis = _settingsService.VisualizationConfig.BoundingBoxSettings.ShowAxis;
    }
    
    partial void OnTransparencyChanged(double value)
    {
        _settingsService.VisualizationConfig.BoundingBoxSettings.Transparency = value;
        UpdateTransparency(value);
    }
    
    partial void OnScaleChanged(double value)
    {
        _settingsService.VisualizationConfig.BoundingBoxSettings.Scale = value;
        UpdateScale(value);
    }
    
    partial void OnSurfaceColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.BoundingBoxSettings.SurfaceColor = value;
        UpdateSurfaceColor(value);
    }
    
    partial void OnEdgeColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.BoundingBoxSettings.EdgeColor = value;
        UpdateEdgeColor(value);
    }
    
    partial void OnAxisColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.BoundingBoxSettings.AxisColor = value;
        UpdateAxisColor(value);
    }
    
    partial void OnShowSurfaceChanged(bool value)
    {
        _settingsService.VisualizationConfig.BoundingBoxSettings.ShowSurface = value;
        UpdateShowSurface(value);
    }
    
    partial void OnShowEdgeChanged(bool value)
    {
        _settingsService.VisualizationConfig.BoundingBoxSettings.ShowEdge = value;
        UpdateShowEdge(value);
    }
    
    partial void OnShowAxisChanged(bool value)
    {
        _settingsService.VisualizationConfig.BoundingBoxSettings.ShowAxis = value;
        UpdateShowAxis(value);
    }
    
    private static void UpdateSurfaceColor(System.Windows.Media.Color value)
    {
        VisualizationController.BoundingBoxVisualizationServer.UpdateSurfaceColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateEdgeColor(System.Windows.Media.Color value)
    {
        VisualizationController.BoundingBoxVisualizationServer.UpdateEdgeColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateAxisColor(System.Windows.Media.Color value)
    {
        VisualizationController.BoundingBoxVisualizationServer.UpdateAxisColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateTransparency(double value)
    {
        VisualizationController.BoundingBoxVisualizationServer.UpdateTransparency(value / 100);
    }
    
    private static void UpdateScale(double value)
    {
        VisualizationController.BoundingBoxVisualizationServer.UpdateScale(value / 100);
    }
    
    private static void UpdateShowSurface(bool value)
    {
        VisualizationController.BoundingBoxVisualizationServer.UpdateSurfaceVisibility(value);
    }
    
    private static void UpdateShowEdge(bool value)
    {
        VisualizationController.BoundingBoxVisualizationServer.UpdateEdgeVisibility(value);
    }
    
    private static void UpdateShowAxis(bool value)
    {
        VisualizationController.BoundingBoxVisualizationServer.UpdateAxisVisibility(value);
    }
}
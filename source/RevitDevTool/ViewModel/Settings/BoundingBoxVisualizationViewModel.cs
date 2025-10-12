using RevitDevTool.Services ;
using RevitDevTool.ViewModel.Contracts ;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings;

public sealed partial class BoundingBoxVisualizationViewModel : ObservableObject, IVisualizationViewModel
{
    public static readonly BoundingBoxVisualizationViewModel Instance = new();
    
    [ObservableProperty] private double _transparency = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.Transparency;
    [ObservableProperty] private double _scale = 100; // Default scale to 100%
    
    [ObservableProperty] private System.Windows.Media.Color _surfaceColor = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.SurfaceColor;
    [ObservableProperty] private System.Windows.Media.Color _edgeColor = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.EdgeColor;
    [ObservableProperty] private System.Windows.Media.Color _axisColor = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.AxisColor;
    
    [ObservableProperty] private bool _showSurface = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.ShowSurface;
    [ObservableProperty] private bool _showEdge = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.ShowEdge;
    [ObservableProperty] private bool _showAxis = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.ShowAxis;
    
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
        Transparency = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.Transparency;
        Scale = 100; // Default scale to 100%
        SurfaceColor = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.SurfaceColor;
        EdgeColor = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.EdgeColor;
        AxisColor = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.AxisColor;
        ShowSurface = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.ShowSurface;
        ShowEdge = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.ShowEdge;
        ShowAxis = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.ShowAxis;
    }
    
    partial void OnTransparencyChanged(double value)
    {
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.Transparency = value;
        UpdateTransparency(value);
    }
    
    partial void OnScaleChanged(double value)
    {
        UpdateScale(value);
    }
    
    partial void OnSurfaceColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.SurfaceColor = value;
        UpdateSurfaceColor(value);
    }
    
    partial void OnEdgeColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.EdgeColor = value;
        UpdateEdgeColor(value);
    }
    
    partial void OnAxisColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.AxisColor = value;
        UpdateAxisColor(value);
    }
    
    partial void OnShowSurfaceChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.ShowSurface = value;
        UpdateShowSurface(value);
    }
    
    partial void OnShowEdgeChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.ShowEdge = value;
        UpdateShowEdge(value);
    }
    
    partial void OnShowAxisChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.ShowEdge = value;
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
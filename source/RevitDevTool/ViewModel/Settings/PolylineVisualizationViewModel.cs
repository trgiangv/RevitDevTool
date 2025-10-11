using RevitDevTool.Services ;
using RevitDevTool.ViewModel.Contracts ;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings;

public sealed partial class PolylineVisualizationViewModel: ObservableObject, IInitialized
{
    public static readonly PolylineVisualizationViewModel Instance = new();
    
    [ObservableProperty] private double _diameter = SettingsService.Instance.VisualizationConfig.PolylineSettings.Diameter;
    [ObservableProperty] private double _transparency = SettingsService.Instance.VisualizationConfig.PolylineSettings.Transparency;
    
    [ObservableProperty] private System.Windows.Media.Color _surfaceColor = SettingsService.Instance.VisualizationConfig.PolylineSettings.SurfaceColor;
    [ObservableProperty] private System.Windows.Media.Color _curveColor = SettingsService.Instance.VisualizationConfig.PolylineSettings.CurveColor;
    [ObservableProperty] private System.Windows.Media.Color _directionColor = SettingsService.Instance.VisualizationConfig.PolylineSettings.DirectionColor;
    
    [ObservableProperty] private bool _showSurface = SettingsService.Instance.VisualizationConfig.PolylineSettings.ShowSurface;
    [ObservableProperty] private bool _showCurve = SettingsService.Instance.VisualizationConfig.PolylineSettings.ShowCurve;
    [ObservableProperty] private bool _showDirection = SettingsService.Instance.VisualizationConfig.PolylineSettings.ShowDirection;
    
    public double MinThickness => SettingsService.Instance.VisualizationConfig.PolylineSettings.MinThickness;
    
    public void Initialize()
    {
        UpdateShowSurface(ShowSurface);
        UpdateShowCurve(ShowCurve);
        UpdateShowDirection(ShowDirection);
        
        UpdateSurfaceColor(SurfaceColor);
        UpdateCurveColor(CurveColor);
        UpdateDirectionColor(DirectionColor);
        
        UpdateTransparency(Transparency);
        UpdateDiameter(Diameter);
    }
    
    partial void OnSurfaceColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.PolylineSettings.SurfaceColor = value;
        UpdateSurfaceColor(value);
    }
    
    partial void OnCurveColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.PolylineSettings.CurveColor = value;
        UpdateCurveColor(value);
    }
    
    partial void OnDirectionColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.PolylineSettings.DirectionColor = value;
        UpdateDirectionColor(value);
    }
    
    partial void OnDiameterChanged(double value)
    {
        SettingsService.Instance.VisualizationConfig.PolylineSettings.Diameter = value;
        UpdateDiameter(value);
    }
    
    partial void OnTransparencyChanged(double value)
    {
        SettingsService.Instance.VisualizationConfig.PolylineSettings.Transparency = value;
        UpdateTransparency(value);
    }
    
    partial void OnShowSurfaceChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.PolylineSettings.ShowSurface = value;
        UpdateShowSurface(value);
    }
    
    partial void OnShowCurveChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.PolylineSettings.ShowCurve = value;
        UpdateShowCurve(value);
    }
    
    partial void OnShowDirectionChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.PolylineSettings.ShowDirection = value;
        UpdateShowDirection(value);
    }
    
    private void UpdateSurfaceColor(System.Windows.Media.Color value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateSurfaceColor(new Color(value.R, value.G, value.B));
    }
    
    private void UpdateCurveColor(System.Windows.Media.Color value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateCurveColor(new Color(value.R, value.G, value.B));
    }
    
    private void UpdateDirectionColor(System.Windows.Media.Color value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateDirectionColor(new Color(value.R, value.G, value.B));
    }
    
    private void UpdateDiameter(double value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateDiameter(value / 12);
    }
    
    private void UpdateTransparency(double value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateTransparency(value / 100);
    }
    
    private void UpdateShowSurface(bool value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateSurfaceVisibility(value);
    }
    
    private void UpdateShowCurve(bool value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateCurveVisibility(value);
    }
    
    private void UpdateShowDirection(bool value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateDirectionVisibility(value);
    }
}
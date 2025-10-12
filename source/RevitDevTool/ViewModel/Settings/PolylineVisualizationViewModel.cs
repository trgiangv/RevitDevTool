using RevitDevTool.Services ;
using RevitDevTool.ViewModel.Contracts ;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings;

public sealed partial class PolylineVisualizationViewModel: ObservableObject, IVisualizationViewModel
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

    public void Refresh()
    {
        Diameter = SettingsService.Instance.VisualizationConfig.PolylineSettings.Diameter;
        Transparency = SettingsService.Instance.VisualizationConfig.PolylineSettings.Transparency;
        
        SurfaceColor = SettingsService.Instance.VisualizationConfig.PolylineSettings.SurfaceColor;
        CurveColor = SettingsService.Instance.VisualizationConfig.PolylineSettings.CurveColor;
        DirectionColor = SettingsService.Instance.VisualizationConfig.PolylineSettings.DirectionColor;
        
        ShowSurface = SettingsService.Instance.VisualizationConfig.PolylineSettings.ShowSurface;
        ShowCurve = SettingsService.Instance.VisualizationConfig.PolylineSettings.ShowCurve;
        ShowDirection = SettingsService.Instance.VisualizationConfig.PolylineSettings.ShowDirection;
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
    
    private static void UpdateSurfaceColor(System.Windows.Media.Color value)
    {
        VisualizationController.PolylineVisualizationServerServer.UpdateSurfaceColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateCurveColor(System.Windows.Media.Color value)
    {
        VisualizationController.PolylineVisualizationServerServer.UpdateCurveColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateDirectionColor(System.Windows.Media.Color value)
    {
        VisualizationController.PolylineVisualizationServerServer.UpdateDirectionColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateDiameter(double value)
    {
        VisualizationController.PolylineVisualizationServerServer.UpdateDiameter(value / 12);
    }
    
    private static void UpdateTransparency(double value)
    {
        VisualizationController.PolylineVisualizationServerServer.UpdateTransparency(value / 100);
    }
    
    private static void UpdateShowSurface(bool value)
    {
        VisualizationController.PolylineVisualizationServerServer.UpdateSurfaceVisibility(value);
    }
    
    private static void UpdateShowCurve(bool value)
    {
        VisualizationController.PolylineVisualizationServerServer.UpdateCurveVisibility(value);
    }
    
    private static void UpdateShowDirection(bool value)
    {
        VisualizationController.PolylineVisualizationServerServer.UpdateDirectionVisibility(value);
    }
}
using RevitDevTool.Controllers;
using RevitDevTool.Services;
using RevitDevTool.ViewModel.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings.Visualization;

public sealed partial class PolylineVisualizationViewModel : ObservableObject, IVisualizationViewModel
{
    private readonly ISettingsService _settingsService;

    public PolylineVisualizationViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        _diameter = _settingsService.VisualizationConfig.PolylineSettings.Diameter;
        _transparency = _settingsService.VisualizationConfig.PolylineSettings.Transparency;
        _surfaceColor = _settingsService.VisualizationConfig.PolylineSettings.SurfaceColor;
        _curveColor = _settingsService.VisualizationConfig.PolylineSettings.CurveColor;
        _directionColor = _settingsService.VisualizationConfig.PolylineSettings.DirectionColor;
        _showSurface = _settingsService.VisualizationConfig.PolylineSettings.ShowSurface;
        _showCurve = _settingsService.VisualizationConfig.PolylineSettings.ShowCurve;
        _showDirection = _settingsService.VisualizationConfig.PolylineSettings.ShowDirection;
    }

    [ObservableProperty] private double _diameter;
    [ObservableProperty] private double _transparency;

    [ObservableProperty] private System.Windows.Media.Color _surfaceColor;
    [ObservableProperty] private System.Windows.Media.Color _curveColor;
    [ObservableProperty] private System.Windows.Media.Color _directionColor;

    [ObservableProperty] private bool _showSurface;
    [ObservableProperty] private bool _showCurve;
    [ObservableProperty] private bool _showDirection;

    public double MinThickness => _settingsService.VisualizationConfig.PolylineSettings.MinThickness;

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
        Diameter = _settingsService.VisualizationConfig.PolylineSettings.Diameter;
        Transparency = _settingsService.VisualizationConfig.PolylineSettings.Transparency;

        SurfaceColor = _settingsService.VisualizationConfig.PolylineSettings.SurfaceColor;
        CurveColor = _settingsService.VisualizationConfig.PolylineSettings.CurveColor;
        DirectionColor = _settingsService.VisualizationConfig.PolylineSettings.DirectionColor;

        ShowSurface = _settingsService.VisualizationConfig.PolylineSettings.ShowSurface;
        ShowCurve = _settingsService.VisualizationConfig.PolylineSettings.ShowCurve;
        ShowDirection = _settingsService.VisualizationConfig.PolylineSettings.ShowDirection;
    }

    partial void OnSurfaceColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.PolylineSettings.SurfaceColor = value;
        UpdateSurfaceColor(value);
    }

    partial void OnCurveColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.PolylineSettings.CurveColor = value;
        UpdateCurveColor(value);
    }

    partial void OnDirectionColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.PolylineSettings.DirectionColor = value;
        UpdateDirectionColor(value);
    }

    partial void OnDiameterChanged(double value)
    {
        _settingsService.VisualizationConfig.PolylineSettings.Diameter = value;
        UpdateDiameter(value);
    }

    partial void OnTransparencyChanged(double value)
    {
        _settingsService.VisualizationConfig.PolylineSettings.Transparency = value;
        UpdateTransparency(value);
    }

    partial void OnShowSurfaceChanged(bool value)
    {
        _settingsService.VisualizationConfig.PolylineSettings.ShowSurface = value;
        UpdateShowSurface(value);
    }

    partial void OnShowCurveChanged(bool value)
    {
        _settingsService.VisualizationConfig.PolylineSettings.ShowCurve = value;
        UpdateShowCurve(value);
    }

    partial void OnShowDirectionChanged(bool value)
    {
        _settingsService.VisualizationConfig.PolylineSettings.ShowDirection = value;
        UpdateShowDirection(value);
    }

    private static void UpdateSurfaceColor(System.Windows.Media.Color value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateSurfaceColor(new Color(value.R, value.G, value.B));
    }

    private static void UpdateCurveColor(System.Windows.Media.Color value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateCurveColor(new Color(value.R, value.G, value.B));
    }

    private static void UpdateDirectionColor(System.Windows.Media.Color value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateDirectionColor(new Color(value.R, value.G, value.B));
    }

    private static void UpdateDiameter(double value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateDiameter(value / 12);
    }

    private static void UpdateTransparency(double value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateTransparency(value / 100);
    }

    private static void UpdateShowSurface(bool value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateSurfaceVisibility(value);
    }

    private static void UpdateShowCurve(bool value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateCurveVisibility(value);
    }

    private static void UpdateShowDirection(bool value)
    {
        VisualizationController.PolylineVisualizationServer.UpdateDirectionVisibility(value);
    }
}
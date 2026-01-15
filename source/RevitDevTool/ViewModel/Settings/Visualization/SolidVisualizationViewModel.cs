using RevitDevTool.Controllers;
using RevitDevTool.Settings;
using RevitDevTool.ViewModel.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings.Visualization;

public sealed partial class SolidVisualizationViewModel : ObservableObject, IVisualizationViewModel
{
    private readonly ISettingsService _settingsService;

    public SolidVisualizationViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        _scale = _settingsService.VisualizationConfig.SolidSettings.Scale;
        _transparency = _settingsService.VisualizationConfig.SolidSettings.Transparency;
        _faceColor = _settingsService.VisualizationConfig.SolidSettings.FaceColor;
        _edgeColor = _settingsService.VisualizationConfig.SolidSettings.EdgeColor;
        _showFace = _settingsService.VisualizationConfig.SolidSettings.ShowFace;
        _showEdge = _settingsService.VisualizationConfig.SolidSettings.ShowEdge;
    }

    [ObservableProperty] private double _scale;
    [ObservableProperty] private double _transparency;

    [ObservableProperty] private System.Windows.Media.Color _faceColor;
    [ObservableProperty] private System.Windows.Media.Color _edgeColor;

    [ObservableProperty] private bool _showFace;
    [ObservableProperty] private bool _showEdge;

    public void Initialize()
    {
        UpdateShowFace(ShowFace);
        UpdateShowEdge(ShowEdge);

        UpdateFaceColor(FaceColor);
        UpdateEdgeColor(EdgeColor);

        UpdateTransparency(Transparency);
        UpdateScale(Scale);
    }

    public void Refresh()
    {
        Scale = _settingsService.VisualizationConfig.SolidSettings.Scale;
        Transparency = _settingsService.VisualizationConfig.SolidSettings.Transparency;
        FaceColor = _settingsService.VisualizationConfig.SolidSettings.FaceColor;
        EdgeColor = _settingsService.VisualizationConfig.SolidSettings.EdgeColor;
        ShowFace = _settingsService.VisualizationConfig.SolidSettings.ShowFace;
        ShowEdge = _settingsService.VisualizationConfig.SolidSettings.ShowEdge;
    }

    partial void OnFaceColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.SolidSettings.FaceColor = value;
        UpdateFaceColor(value);
    }

    partial void OnEdgeColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.SolidSettings.EdgeColor = value;
        UpdateEdgeColor(value);
    }

    partial void OnTransparencyChanged(double value)
    {
        _settingsService.VisualizationConfig.SolidSettings.Transparency = value;
        UpdateTransparency(value);
    }

    partial void OnScaleChanged(double value)
    {
        _settingsService.VisualizationConfig.SolidSettings.Scale = value;
        UpdateScale(value);
    }

    partial void OnShowFaceChanged(bool value)
    {
        _settingsService.VisualizationConfig.SolidSettings.ShowFace = value;
        UpdateShowFace(value);
    }

    partial void OnShowEdgeChanged(bool value)
    {
        _settingsService.VisualizationConfig.SolidSettings.ShowEdge = value;
        UpdateShowEdge(value);
    }

    private static void UpdateFaceColor(System.Windows.Media.Color value)
    {
        VisualizationController.SolidVisualizationServer.UpdateSurfaceColor(new Color(value.R, value.G, value.B));
    }

    private static void UpdateEdgeColor(System.Windows.Media.Color value)
    {
        VisualizationController.SolidVisualizationServer.UpdateEdgeColor(new Color(value.R, value.G, value.B));
    }

    private static void UpdateTransparency(double value)
    {
        VisualizationController.SolidVisualizationServer.UpdateTransparency(value / 100);
    }

    private static void UpdateScale(double value)
    {
        VisualizationController.SolidVisualizationServer.UpdateScale(value / 100);
    }

    private static void UpdateShowFace(bool value)
    {
        VisualizationController.SolidVisualizationServer.UpdateFaceVisibility(value);
    }

    private static void UpdateShowEdge(bool value)
    {
        VisualizationController.SolidVisualizationServer.UpdateEdgeVisibility(value);
    }
}
using RevitDevTool.Controllers;
using RevitDevTool.Settings;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings.Visualization;

public sealed partial class SolidVisualizationViewModel : ObservableObject, IVisualizationViewModel
{
    private readonly IRevitSettingsService _settingsService;

    public SolidVisualizationViewModel(IRevitSettingsService settingsService)
    {
        _settingsService = settingsService;
        Scale = _settingsService.VisualizationConfig.SolidSettings.Scale;
        Transparency = _settingsService.VisualizationConfig.SolidSettings.Transparency;
        FaceColor = _settingsService.VisualizationConfig.SolidSettings.FaceColor;
        EdgeColor = _settingsService.VisualizationConfig.SolidSettings.EdgeColor;
        ShowFace = _settingsService.VisualizationConfig.SolidSettings.ShowFace;
        ShowEdge = _settingsService.VisualizationConfig.SolidSettings.ShowEdge;
    }

    [ObservableProperty]
    public partial double Scale { get; set; }

    [ObservableProperty]
    public partial double Transparency { get; set; }

    [ObservableProperty]
    public partial System.Windows.Media.Color FaceColor { get; set; }

    [ObservableProperty]
    public partial System.Windows.Media.Color EdgeColor { get; set; }

    [ObservableProperty]
    public partial bool ShowFace { get; set; }

    [ObservableProperty]
    public partial bool ShowEdge { get; set; }

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
using RevitDevTool.Services ;
using RevitDevTool.ViewModel.Contracts ;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings;

public sealed partial class SolidVisualizationViewModel: ObservableObject, IInitialized
{
    public static readonly SolidVisualizationViewModel Instance = new();
    
    [ObservableProperty] private double _scale = SettingsService.Instance.VisualizationConfig.SolidSettings.Scale;
    [ObservableProperty] private double _transparency = SettingsService.Instance.VisualizationConfig.SolidSettings.Transparency;

    [ObservableProperty] private System.Windows.Media.Color _faceColor = SettingsService.Instance.VisualizationConfig.SolidSettings.FaceColor;
    [ObservableProperty] private System.Windows.Media.Color _edgeColor = SettingsService.Instance.VisualizationConfig.SolidSettings.EdgeColor;

    [ObservableProperty] private bool _showFace = SettingsService.Instance.VisualizationConfig.SolidSettings.ShowFace;
    [ObservableProperty] private bool _showEdge = SettingsService.Instance.VisualizationConfig.SolidSettings.ShowEdge;

    public void Initialize()
    {
        UpdateShowFace(ShowFace);
        UpdateShowEdge(ShowEdge);

        UpdateFaceColor(FaceColor);
        UpdateEdgeColor(EdgeColor);

        UpdateTransparency(Transparency);
        UpdateScale(Scale);
    }
    
    partial void OnFaceColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.SolidSettings.FaceColor = value;
        UpdateFaceColor(value);
    }

    partial void OnEdgeColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.SolidSettings.EdgeColor = value;
        UpdateEdgeColor(value);
    }

    partial void OnTransparencyChanged(double value)
    {
        SettingsService.Instance.VisualizationConfig.SolidSettings.Transparency = value;
        UpdateTransparency(value);
    }

    partial void OnScaleChanged(double value)
    {
        SettingsService.Instance.VisualizationConfig.SolidSettings.Scale = value;
        UpdateScale(value);
    }

    partial void OnShowFaceChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.SolidSettings.ShowFace = value;
        UpdateShowFace(value);
    }

    partial void OnShowEdgeChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.SolidSettings.ShowEdge = value;
        UpdateShowEdge(value);
    }

    private void UpdateFaceColor(System.Windows.Media.Color value)
    {
        VisualizationController.SolidVisualizationServer.UpdateFaceColor(new Color(value.R, value.G, value.B));
    }

    private void UpdateEdgeColor(System.Windows.Media.Color value)
    {
        VisualizationController.SolidVisualizationServer.UpdateEdgeColor(new Color(value.R, value.G, value.B));
    }

    private void UpdateTransparency(double value)
    {
        VisualizationController.SolidVisualizationServer.UpdateTransparency(value / 100);
    }

    private void UpdateScale(double value)
    {
        VisualizationController.SolidVisualizationServer.UpdateScale(value / 100);
    }

    private void UpdateShowFace(bool value)
    {
        VisualizationController.SolidVisualizationServer.UpdateFaceVisibility(value);
    }

    private void UpdateShowEdge(bool value)
    {
        VisualizationController.SolidVisualizationServer.UpdateEdgeVisibility(value);
    }
}
using RevitDevTool.Services ;
using RevitDevTool.ViewModel.Contracts ;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings;

public sealed partial class FaceVisualizationViewModel : ObservableObject, IInitialized
{
    public static readonly FaceVisualizationViewModel Instance = new();
    
    [ObservableProperty] private double _extrusion = SettingsService.Instance.VisualizationConfig.FaceSettings.Extrusion;
    [ObservableProperty] private double _transparency = SettingsService.Instance.VisualizationConfig.FaceSettings.Transparency;
    
    [ObservableProperty] private System.Windows.Media.Color _surfaceColor = SettingsService.Instance.VisualizationConfig.FaceSettings.SurfaceColor;
    [ObservableProperty] private System.Windows.Media.Color _meshColor = SettingsService.Instance.VisualizationConfig.FaceSettings.MeshColor;
    [ObservableProperty] private System.Windows.Media.Color _normalVectorColor = SettingsService.Instance.VisualizationConfig.FaceSettings.NormalVectorColor;
    
    [ObservableProperty] private bool _showSurface = SettingsService.Instance.VisualizationConfig.FaceSettings.ShowSurface;
    [ObservableProperty] private bool _showMeshGrid = SettingsService.Instance.VisualizationConfig.FaceSettings.ShowMeshGrid;
    [ObservableProperty] private bool _showNormalVector = SettingsService.Instance.VisualizationConfig.FaceSettings.ShowNormalVector;
    
    public double MinExtrusion => SettingsService.Instance.VisualizationConfig.FaceSettings.MinExtrusion;
    
    public void Initialize()
    {
        UpdateShowSurface(ShowSurface);
        UpdateShowMeshGrid(ShowMeshGrid);
        UpdateShowNormalVector(ShowNormalVector);
        
        UpdateSurfaceColor(SurfaceColor);
        UpdateMeshColor(MeshColor);
        UpdateNormalVectorColor(NormalVectorColor);
        
        UpdateExtrusion(Extrusion);
        UpdateTransparency(Transparency);
    }
    
    partial void OnSurfaceColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.FaceSettings.SurfaceColor = value;
        UpdateSurfaceColor(value);
    }
    
    partial void OnMeshColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.FaceSettings.MeshColor = value;
        UpdateMeshColor(value);
    }
    
    partial void OnNormalVectorColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.FaceSettings.NormalVectorColor = value;
        UpdateNormalVectorColor(value);
    }
    
    partial void OnExtrusionChanged(double value)
    {
        SettingsService.Instance.VisualizationConfig.FaceSettings.Extrusion = value;
        UpdateExtrusion(value);
    }
    
    partial void OnTransparencyChanged(double value)
    {
        SettingsService.Instance.VisualizationConfig.FaceSettings.Transparency = value;
        UpdateTransparency(value);
    }
    
    partial void OnShowSurfaceChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.FaceSettings.ShowSurface = value;
        UpdateShowSurface(value);
    }
    
    partial void OnShowMeshGridChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.FaceSettings.ShowMeshGrid = value;
        UpdateShowMeshGrid(value);
    }
    
    partial void OnShowNormalVectorChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.FaceSettings.ShowNormalVector = value;
        UpdateShowNormalVector(value);
    }
    
    private void UpdateSurfaceColor(System.Windows.Media.Color value)
    {
        VisualizationController.FaceVisualizationServer.UpdateSurfaceColor(new Color(value.R, value.G, value.B));
    }
    
    private void UpdateMeshColor(System.Windows.Media.Color value)
    {
        VisualizationController.FaceVisualizationServer.UpdateMeshGridColor(new Color(value.R, value.G, value.B));
    }
    
    private void UpdateNormalVectorColor(System.Windows.Media.Color value)
    {
        VisualizationController.FaceVisualizationServer.UpdateNormalVectorColor(new Color(value.R, value.G, value.B));
    }
    
    private void UpdateExtrusion(double value)
    {
        VisualizationController.FaceVisualizationServer.UpdateExtrusion(value / 12);
    }
    
    private void UpdateTransparency(double value)
    {
        VisualizationController.FaceVisualizationServer.UpdateTransparency(value / 100);
    }
    
    private void UpdateShowSurface(bool value)
    {
        VisualizationController.FaceVisualizationServer.UpdateSurfaceVisibility(value);
    }
    
    private void UpdateShowMeshGrid(bool value)
    {
        VisualizationController.FaceVisualizationServer.UpdateMeshGridVisibility(value);
    }
    
    private void UpdateShowNormalVector(bool value)
    {
        VisualizationController.FaceVisualizationServer.UpdateNormalVectorVisibility(value);
    }
}
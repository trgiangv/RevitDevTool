using RevitDevTool.Services ;
using RevitDevTool.ViewModel.Contracts ;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings;

public sealed partial class MeshVisualizationViewModel: ObservableObject, IInitialized
{
    public static readonly MeshVisualizationViewModel Instance = new();
    
    [ObservableProperty] private double _extrusion = SettingsService.Instance.VisualizationConfig.MeshSettings.Extrusion;
    [ObservableProperty] private double _transparency = SettingsService.Instance.VisualizationConfig.MeshSettings.Transparency;
    
    [ObservableProperty] private System.Windows.Media.Color _surfaceColor = SettingsService.Instance.VisualizationConfig.MeshSettings.SurfaceColor;
    [ObservableProperty] private System.Windows.Media.Color _meshColor = SettingsService.Instance.VisualizationConfig.MeshSettings.MeshColor;
    [ObservableProperty] private System.Windows.Media.Color _normalVectorColor = SettingsService.Instance.VisualizationConfig.MeshSettings.NormalVectorColor;
    
    [ObservableProperty] private bool _showSurface = SettingsService.Instance.VisualizationConfig.MeshSettings.ShowSurface;
    [ObservableProperty] private bool _showMeshGrid = SettingsService.Instance.VisualizationConfig.MeshSettings.ShowMeshGrid;
    [ObservableProperty] private bool _showNormalVector = SettingsService.Instance.VisualizationConfig.MeshSettings.ShowNormalVector;
    
    public double MinExtrusion => SettingsService.Instance.VisualizationConfig.MeshSettings.MinExtrusion;
    
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
        SettingsService.Instance.VisualizationConfig.MeshSettings.SurfaceColor = value;
        UpdateSurfaceColor(value);
    }
    
    partial void OnMeshColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.MeshSettings.MeshColor = value;
        UpdateMeshColor(value);
    }
    
    partial void OnNormalVectorColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.MeshSettings.NormalVectorColor = value;
        UpdateNormalVectorColor(value);
    }
    
    partial void OnExtrusionChanged(double value)
    {
        SettingsService.Instance.VisualizationConfig.MeshSettings.Extrusion = value;
        UpdateExtrusion(value);
    }
    
    partial void OnTransparencyChanged(double value)
    {
        SettingsService.Instance.VisualizationConfig.MeshSettings.Transparency = value;
        UpdateTransparency(value);
    }
    
    partial void OnShowSurfaceChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.MeshSettings.ShowSurface = value;
        UpdateShowSurface(value);
    }
    
    partial void OnShowMeshGridChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.MeshSettings.ShowMeshGrid = value;
        UpdateShowMeshGrid(value);
    }
    
    partial void OnShowNormalVectorChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.MeshSettings.ShowNormalVector = value;
        UpdateShowNormalVector(value);
    }
    
    private void UpdateSurfaceColor(System.Windows.Media.Color value)
    {
        VisualizationController.MeshVisualizationServer.UpdateSurfaceColor(new Color(value.R, value.G, value.B));
    }
    
    private void UpdateMeshColor(System.Windows.Media.Color value)
    {
        VisualizationController.MeshVisualizationServer.UpdateMeshGridColor(new Color(value.R, value.G, value.B));
    }
    
    private void UpdateNormalVectorColor(System.Windows.Media.Color value)
    {
        VisualizationController.MeshVisualizationServer.UpdateNormalVectorColor(new Color(value.R, value.G, value.B));
    }
    
    private void UpdateExtrusion(double value)
    {
        VisualizationController.MeshVisualizationServer.UpdateExtrusion(value / 12);
    }
    
    private void UpdateTransparency(double value)
    {
        VisualizationController.MeshVisualizationServer.UpdateTransparency(value / 100);
    }
    
    private void UpdateShowSurface(bool value)
    {
        VisualizationController.MeshVisualizationServer.UpdateSurfaceVisibility(value);
    }
    
    private void UpdateShowMeshGrid(bool value)
    {
        VisualizationController.MeshVisualizationServer.UpdateMeshGridVisibility(value);
    }
    
    private void UpdateShowNormalVector(bool value)
    {
        VisualizationController.MeshVisualizationServer.UpdateNormalVectorVisibility(value);
    }
}
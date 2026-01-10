using RevitDevTool.Controllers;
using RevitDevTool.Services;
using RevitDevTool.ViewModel.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings.Visualization;

public sealed partial class FaceVisualizationViewModel : ObservableObject, IVisualizationViewModel
{
    private readonly ISettingsService _settingsService;
    
    public FaceVisualizationViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        _extrusion = _settingsService.VisualizationConfig.FaceSettings.Extrusion;
        _transparency = _settingsService.VisualizationConfig.FaceSettings.Transparency;
        _surfaceColor = _settingsService.VisualizationConfig.FaceSettings.SurfaceColor;
        _meshColor = _settingsService.VisualizationConfig.FaceSettings.MeshColor;
        _normalVectorColor = _settingsService.VisualizationConfig.FaceSettings.NormalVectorColor;
        _showSurface = _settingsService.VisualizationConfig.FaceSettings.ShowSurface;
        _showMeshGrid = _settingsService.VisualizationConfig.FaceSettings.ShowMeshGrid;
        _showNormalVector = _settingsService.VisualizationConfig.FaceSettings.ShowNormalVector;
    }
    
    [ObservableProperty] private double _extrusion;
    [ObservableProperty] private double _transparency;
    
    [ObservableProperty] private System.Windows.Media.Color _surfaceColor;
    [ObservableProperty] private System.Windows.Media.Color _meshColor;
    [ObservableProperty] private System.Windows.Media.Color _normalVectorColor;
    
    [ObservableProperty] private bool _showSurface;
    [ObservableProperty] private bool _showMeshGrid;
    [ObservableProperty] private bool _showNormalVector;
    
    public double MinExtrusion => _settingsService.VisualizationConfig.FaceSettings.MinExtrusion;
    
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

    public void Refresh()
    {
        Extrusion = _settingsService.VisualizationConfig.FaceSettings.Extrusion;
        Transparency = _settingsService.VisualizationConfig.FaceSettings.Transparency;
        SurfaceColor = _settingsService.VisualizationConfig.FaceSettings.SurfaceColor;
        MeshColor = _settingsService.VisualizationConfig.FaceSettings.MeshColor;
        NormalVectorColor = _settingsService.VisualizationConfig.FaceSettings.NormalVectorColor;
        ShowSurface = _settingsService.VisualizationConfig.FaceSettings.ShowSurface;
        ShowMeshGrid = _settingsService.VisualizationConfig.FaceSettings.ShowMeshGrid;
        ShowNormalVector = _settingsService.VisualizationConfig.FaceSettings.ShowNormalVector;
    }

    partial void OnSurfaceColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.FaceSettings.SurfaceColor = value;
        UpdateSurfaceColor(value);
    }
    
    partial void OnMeshColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.FaceSettings.MeshColor = value;
        UpdateMeshColor(value);
    }
    
    partial void OnNormalVectorColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.FaceSettings.NormalVectorColor = value;
        UpdateNormalVectorColor(value);
    }
    
    partial void OnExtrusionChanged(double value)
    {
        _settingsService.VisualizationConfig.FaceSettings.Extrusion = value;
        UpdateExtrusion(value);
    }
    
    partial void OnTransparencyChanged(double value)
    {
        _settingsService.VisualizationConfig.FaceSettings.Transparency = value;
        UpdateTransparency(value);
    }
    
    partial void OnShowSurfaceChanged(bool value)
    {
        _settingsService.VisualizationConfig.FaceSettings.ShowSurface = value;
        UpdateShowSurface(value);
    }
    
    partial void OnShowMeshGridChanged(bool value)
    {
        _settingsService.VisualizationConfig.FaceSettings.ShowMeshGrid = value;
        UpdateShowMeshGrid(value);
    }
    
    partial void OnShowNormalVectorChanged(bool value)
    {
        _settingsService.VisualizationConfig.FaceSettings.ShowNormalVector = value;
        UpdateShowNormalVector(value);
    }
    
    private static void UpdateSurfaceColor(System.Windows.Media.Color value)
    {
        VisualizationController.FaceVisualizationServer.UpdateSurfaceColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateMeshColor(System.Windows.Media.Color value)
    {
        VisualizationController.FaceVisualizationServer.UpdateMeshGridColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateNormalVectorColor(System.Windows.Media.Color value)
    {
        VisualizationController.FaceVisualizationServer.UpdateNormalVectorColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateExtrusion(double value)
    {
        VisualizationController.FaceVisualizationServer.UpdateExtrusion(value / 12);
    }
    
    private static void UpdateTransparency(double value)
    {
        VisualizationController.FaceVisualizationServer.UpdateTransparency(value / 100);
    }
    
    private static void UpdateShowSurface(bool value)
    {
        VisualizationController.FaceVisualizationServer.UpdateSurfaceVisibility(value);
    }
    
    private static void UpdateShowMeshGrid(bool value)
    {
        VisualizationController.FaceVisualizationServer.UpdateMeshGridVisibility(value);
    }
    
    private static void UpdateShowNormalVector(bool value)
    {
        VisualizationController.FaceVisualizationServer.UpdateNormalVectorVisibility(value);
    }
}
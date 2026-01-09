using RevitDevTool.Services;
using RevitDevTool.ViewModel.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings.Visualization;

public sealed partial class MeshVisualizationViewModel: ObservableObject, IVisualizationViewModel
{
    private readonly ISettingsService _settingsService;
    
    public MeshVisualizationViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        _extrusion = _settingsService.VisualizationConfig.MeshSettings.Extrusion;
        _transparency = _settingsService.VisualizationConfig.MeshSettings.Transparency;
        _surfaceColor = _settingsService.VisualizationConfig.MeshSettings.SurfaceColor;
        _meshColor = _settingsService.VisualizationConfig.MeshSettings.MeshColor;
        _normalVectorColor = _settingsService.VisualizationConfig.MeshSettings.NormalVectorColor;
        _showSurface = _settingsService.VisualizationConfig.MeshSettings.ShowSurface;
        _showMeshGrid = _settingsService.VisualizationConfig.MeshSettings.ShowMeshGrid;
        _showNormalVector = _settingsService.VisualizationConfig.MeshSettings.ShowNormalVector;
    }
    
    [ObservableProperty] private double _extrusion;
    [ObservableProperty] private double _transparency;
    
    [ObservableProperty] private System.Windows.Media.Color _surfaceColor;
    [ObservableProperty] private System.Windows.Media.Color _meshColor;
    [ObservableProperty] private System.Windows.Media.Color _normalVectorColor;
    
    [ObservableProperty] private bool _showSurface;
    [ObservableProperty] private bool _showMeshGrid;
    [ObservableProperty] private bool _showNormalVector;
    
    public double MinExtrusion => _settingsService.VisualizationConfig.MeshSettings.MinExtrusion;
    
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
        Extrusion = _settingsService.VisualizationConfig.MeshSettings.Extrusion;
        Transparency = _settingsService.VisualizationConfig.MeshSettings.Transparency;
        
        SurfaceColor = _settingsService.VisualizationConfig.MeshSettings.SurfaceColor;
        MeshColor = _settingsService.VisualizationConfig.MeshSettings.MeshColor;
        NormalVectorColor = _settingsService.VisualizationConfig.MeshSettings.NormalVectorColor;
        
        ShowSurface = _settingsService.VisualizationConfig.MeshSettings.ShowSurface;
        ShowMeshGrid = _settingsService.VisualizationConfig.MeshSettings.ShowMeshGrid;
        ShowNormalVector = _settingsService.VisualizationConfig.MeshSettings.ShowNormalVector;
    }

    partial void OnSurfaceColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.MeshSettings.SurfaceColor = value;
        UpdateSurfaceColor(value);
    }
    
    partial void OnMeshColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.MeshSettings.MeshColor = value;
        UpdateMeshColor(value);
    }
    
    partial void OnNormalVectorColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.MeshSettings.NormalVectorColor = value;
        UpdateNormalVectorColor(value);
    }
    
    partial void OnExtrusionChanged(double value)
    {
        _settingsService.VisualizationConfig.MeshSettings.Extrusion = value;
        UpdateExtrusion(value);
    }
    
    partial void OnTransparencyChanged(double value)
    {
        _settingsService.VisualizationConfig.MeshSettings.Transparency = value;
        UpdateTransparency(value);
    }
    
    partial void OnShowSurfaceChanged(bool value)
    {
        _settingsService.VisualizationConfig.MeshSettings.ShowSurface = value;
        UpdateShowSurface(value);
    }
    
    partial void OnShowMeshGridChanged(bool value)
    {
        _settingsService.VisualizationConfig.MeshSettings.ShowMeshGrid = value;
        UpdateShowMeshGrid(value);
    }
    
    partial void OnShowNormalVectorChanged(bool value)
    {
        _settingsService.VisualizationConfig.MeshSettings.ShowNormalVector = value;
        UpdateShowNormalVector(value);
    }
    
    private static void UpdateSurfaceColor(System.Windows.Media.Color value)
    {
        VisualizationController.MeshVisualizationServer.UpdateSurfaceColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateMeshColor(System.Windows.Media.Color value)
    {
        VisualizationController.MeshVisualizationServer.UpdateMeshGridColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateNormalVectorColor(System.Windows.Media.Color value)
    {
        VisualizationController.MeshVisualizationServer.UpdateNormalVectorColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateExtrusion(double value)
    {
        VisualizationController.MeshVisualizationServer.UpdateExtrusion(value / 12);
    }
    
    private static void UpdateTransparency(double value)
    {
        VisualizationController.MeshVisualizationServer.UpdateTransparency(value / 100);
    }
    
    private static void UpdateShowSurface(bool value)
    {
        VisualizationController.MeshVisualizationServer.UpdateSurfaceVisibility(value);
    }
    
    private static void UpdateShowMeshGrid(bool value)
    {
        VisualizationController.MeshVisualizationServer.UpdateMeshGridVisibility(value);
    }
    
    private static void UpdateShowNormalVector(bool value)
    {
        VisualizationController.MeshVisualizationServer.UpdateNormalVectorVisibility(value);
    }
}
using RevitDevTool.Services ;
using RevitDevTool.ViewModel.Contracts ;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings;

public sealed partial class XyzVisualizationViewModel: ObservableObject, IInitialized
{
    public static readonly XyzVisualizationViewModel Instance = new();
    
    [ObservableProperty] private double _axisLength = SettingsService.Instance.VisualizationConfig.XyzSettings.AxisLength;
    [ObservableProperty] private double _transparency = SettingsService.Instance.VisualizationConfig.XyzSettings.Transparency;
    
    [ObservableProperty] private System.Windows.Media.Color _xColor = SettingsService.Instance.VisualizationConfig.XyzSettings.XColor;
    [ObservableProperty] private System.Windows.Media.Color _yColor = SettingsService.Instance.VisualizationConfig.XyzSettings.YColor;
    [ObservableProperty] private System.Windows.Media.Color _zColor = SettingsService.Instance.VisualizationConfig.XyzSettings.ZColor;
    
    [ObservableProperty] private bool _showPlane = SettingsService.Instance.VisualizationConfig.XyzSettings.ShowPlane;
    [ObservableProperty] private bool _showXAxis = SettingsService.Instance.VisualizationConfig.XyzSettings.ShowXAxis;
    [ObservableProperty] private bool _showYAxis = SettingsService.Instance.VisualizationConfig.XyzSettings.ShowYAxis;
    [ObservableProperty] private bool _showZAxis = SettingsService.Instance.VisualizationConfig.XyzSettings.ShowZAxis;
    
    public double MinAxisLength => SettingsService.Instance.VisualizationConfig.XyzSettings.MinAxisLength;
    
    public void Initialize()
    {
        UpdateShowPlane(ShowPlane);
        UpdateShowXAxis(ShowXAxis);
        UpdateShowYAxis(ShowYAxis);
        UpdateShowZAxis(ShowZAxis);
        
        UpdateXColor(XColor);
        UpdateYColor(YColor);
        UpdateZColor(ZColor);
        
        UpdateAxisLength(AxisLength);
        UpdateTransparency(Transparency);
    }
    
    partial void OnXColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.XyzSettings.XColor = value;
        UpdateXColor(value);
    }
    
    partial void OnYColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.XyzSettings.YColor = value;
        UpdateYColor(value);
    }
    
    partial void OnZColorChanged(System.Windows.Media.Color value)
    {
        SettingsService.Instance.VisualizationConfig.XyzSettings.ZColor = value;
        UpdateZColor(value);
    }
    
    partial void OnAxisLengthChanged(double value)
    {
        SettingsService.Instance.VisualizationConfig.XyzSettings.AxisLength = value;
        UpdateAxisLength(value);
    }
    
    partial void OnTransparencyChanged(double value)
    {
        SettingsService.Instance.VisualizationConfig.XyzSettings.Transparency = value;
        UpdateTransparency(value);
    }
    
    partial void OnShowPlaneChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.XyzSettings.ShowPlane = value;
        UpdateShowPlane(value);
    }
    
    partial void OnShowXAxisChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.XyzSettings.ShowXAxis = value;
        UpdateShowXAxis(value);
    }
    
    partial void OnShowYAxisChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.XyzSettings.ShowYAxis = value;
        UpdateShowYAxis(value);
    }
    
    partial void OnShowZAxisChanged(bool value)
    {
        SettingsService.Instance.VisualizationConfig.XyzSettings.ShowZAxis = value;
        UpdateShowZAxis(value);
    }
    
    private void UpdateXColor(System.Windows.Media.Color value)
    {
        VisualizationController.XyzVisualizationServer.UpdateXColor(new Color(value.R, value.G, value.B));
    }
    
    private void UpdateYColor(System.Windows.Media.Color value)
    {
        VisualizationController.XyzVisualizationServer.UpdateYColor(new Color(value.R, value.G, value.B));
    }
    
    private void UpdateZColor(System.Windows.Media.Color value)
    {
        VisualizationController.XyzVisualizationServer.UpdateZColor(new Color(value.R, value.G, value.B));
    }
    
    private void UpdateAxisLength(double value)
    {
        VisualizationController.XyzVisualizationServer.UpdateAxisLength(value / 12);
    }
    
    private void UpdateTransparency(double value)
    {
        VisualizationController.XyzVisualizationServer.UpdateTransparency(value / 100);
    }
    
    private void UpdateShowPlane(bool value)
    {
        VisualizationController.XyzVisualizationServer.UpdatePlaneVisibility(value);
    }
    
    private void UpdateShowXAxis(bool value)
    {
        VisualizationController.XyzVisualizationServer.UpdateXAxisVisibility(value);
    }
    
    private void UpdateShowYAxis(bool value)
    {
        VisualizationController.XyzVisualizationServer.UpdateYAxisVisibility(value);
    }
    
    private void UpdateShowZAxis(bool value)
    {
        VisualizationController.XyzVisualizationServer.UpdateZAxisVisibility(value);
    }
}
using RevitDevTool.Services ;
using RevitDevTool.ViewModel.Contracts ;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings;

public sealed partial class XyzVisualizationViewModel: ObservableObject, IVisualizationViewModel
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

    public void Refresh()
    {
        AxisLength = SettingsService.Instance.VisualizationConfig.XyzSettings.AxisLength;
        Transparency = SettingsService.Instance.VisualizationConfig.XyzSettings.Transparency;
        
        XColor = SettingsService.Instance.VisualizationConfig.XyzSettings.XColor;
        YColor = SettingsService.Instance.VisualizationConfig.XyzSettings.YColor;
        ZColor = SettingsService.Instance.VisualizationConfig.XyzSettings.ZColor;
        
        ShowPlane = SettingsService.Instance.VisualizationConfig.XyzSettings.ShowPlane;
        ShowXAxis = SettingsService.Instance.VisualizationConfig.XyzSettings.ShowXAxis;
        ShowYAxis = SettingsService.Instance.VisualizationConfig.XyzSettings.ShowYAxis;
        ShowZAxis = SettingsService.Instance.VisualizationConfig.XyzSettings.ShowZAxis;
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
    
    private static void UpdateXColor(System.Windows.Media.Color value)
    {
        VisualizationController.XyzVisualizationServer.UpdateXColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateYColor(System.Windows.Media.Color value)
    {
        VisualizationController.XyzVisualizationServer.UpdateYColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateZColor(System.Windows.Media.Color value)
    {
        VisualizationController.XyzVisualizationServer.UpdateZColor(new Color(value.R, value.G, value.B));
    }
    
    private static void UpdateAxisLength(double value)
    {
        VisualizationController.XyzVisualizationServer.UpdateAxisLength(value / 12);
    }
    
    private static void UpdateTransparency(double value)
    {
        VisualizationController.XyzVisualizationServer.UpdateTransparency(value / 100);
    }
    
    private static void UpdateShowPlane(bool value)
    {
        VisualizationController.XyzVisualizationServer.UpdatePlaneVisibility(value);
    }
    
    private static void UpdateShowXAxis(bool value)
    {
        VisualizationController.XyzVisualizationServer.UpdateXAxisVisibility(value);
    }
    
    private static void UpdateShowYAxis(bool value)
    {
        VisualizationController.XyzVisualizationServer.UpdateYAxisVisibility(value);
    }
    
    private static void UpdateShowZAxis(bool value)
    {
        VisualizationController.XyzVisualizationServer.UpdateZAxisVisibility(value);
    }
}
using RevitDevTool.Controllers;
using RevitDevTool.Services;
using RevitDevTool.ViewModel.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.ViewModel.Settings.Visualization;

public sealed partial class XyzVisualizationViewModel : ObservableObject, IVisualizationViewModel
{
    private readonly ISettingsService _settingsService;

    public XyzVisualizationViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        _axisLength = _settingsService.VisualizationConfig.XyzSettings.AxisLength;
        _transparency = _settingsService.VisualizationConfig.XyzSettings.Transparency;
        _xColor = _settingsService.VisualizationConfig.XyzSettings.XColor;
        _yColor = _settingsService.VisualizationConfig.XyzSettings.YColor;
        _zColor = _settingsService.VisualizationConfig.XyzSettings.ZColor;
        _showPlane = _settingsService.VisualizationConfig.XyzSettings.ShowPlane;
        _showXAxis = _settingsService.VisualizationConfig.XyzSettings.ShowXAxis;
        _showYAxis = _settingsService.VisualizationConfig.XyzSettings.ShowYAxis;
        _showZAxis = _settingsService.VisualizationConfig.XyzSettings.ShowZAxis;
    }

    [ObservableProperty] private double _axisLength;
    [ObservableProperty] private double _transparency;

    [ObservableProperty] private System.Windows.Media.Color _xColor;
    [ObservableProperty] private System.Windows.Media.Color _yColor;
    [ObservableProperty] private System.Windows.Media.Color _zColor;

    [ObservableProperty] private bool _showPlane;
    [ObservableProperty] private bool _showXAxis;
    [ObservableProperty] private bool _showYAxis;
    [ObservableProperty] private bool _showZAxis;

    public double MinAxisLength => _settingsService.VisualizationConfig.XyzSettings.MinAxisLength;

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
        AxisLength = _settingsService.VisualizationConfig.XyzSettings.AxisLength;
        Transparency = _settingsService.VisualizationConfig.XyzSettings.Transparency;

        XColor = _settingsService.VisualizationConfig.XyzSettings.XColor;
        YColor = _settingsService.VisualizationConfig.XyzSettings.YColor;
        ZColor = _settingsService.VisualizationConfig.XyzSettings.ZColor;

        ShowPlane = _settingsService.VisualizationConfig.XyzSettings.ShowPlane;
        ShowXAxis = _settingsService.VisualizationConfig.XyzSettings.ShowXAxis;
        ShowYAxis = _settingsService.VisualizationConfig.XyzSettings.ShowYAxis;
        ShowZAxis = _settingsService.VisualizationConfig.XyzSettings.ShowZAxis;
    }

    partial void OnXColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.XyzSettings.XColor = value;
        UpdateXColor(value);
    }

    partial void OnYColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.XyzSettings.YColor = value;
        UpdateYColor(value);
    }

    partial void OnZColorChanged(System.Windows.Media.Color value)
    {
        _settingsService.VisualizationConfig.XyzSettings.ZColor = value;
        UpdateZColor(value);
    }

    partial void OnAxisLengthChanged(double value)
    {
        _settingsService.VisualizationConfig.XyzSettings.AxisLength = value;
        UpdateAxisLength(value);
    }

    partial void OnTransparencyChanged(double value)
    {
        _settingsService.VisualizationConfig.XyzSettings.Transparency = value;
        UpdateTransparency(value);
    }

    partial void OnShowPlaneChanged(bool value)
    {
        _settingsService.VisualizationConfig.XyzSettings.ShowPlane = value;
        UpdateShowPlane(value);
    }

    partial void OnShowXAxisChanged(bool value)
    {
        _settingsService.VisualizationConfig.XyzSettings.ShowXAxis = value;
        UpdateShowXAxis(value);
    }

    partial void OnShowYAxisChanged(bool value)
    {
        _settingsService.VisualizationConfig.XyzSettings.ShowYAxis = value;
        UpdateShowYAxis(value);
    }

    partial void OnShowZAxisChanged(bool value)
    {
        _settingsService.VisualizationConfig.XyzSettings.ShowZAxis = value;
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
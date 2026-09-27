using HandyControl.Controls;
using HandyControl.Data;
using RevitDevTool.View.Settings.Visualization;
using System.Windows;

namespace RevitDevTool.View.Settings;

public partial class VisualizationSettingsView
{
    private readonly Dictionary<Type, object> _viewCache = [];

    public VisualizationSettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (FindSelectedSideMenuItem() is { } item)
        {
            NavigateTo(item.Tag?.ToString());
        }
    }

    private void OnSideMenuSelectionChanged(object sender, FunctionEventArgs<object> e)
    {
        if (e.Info is SideMenuItem item)
        {
            NavigateTo(item.Tag?.ToString());
        }
    }

    private SideMenuItem? FindSelectedSideMenuItem()
    {
        foreach (var child in SideMenuControl.Items)
        {
            if (child is SideMenuItem { IsSelected: true } selected)
            {
                return selected;
            }
        }

        return SideMenuControl.Items.OfType<SideMenuItem>().FirstOrDefault();
    }

    private void NavigateTo(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return;

        var viewType = tag switch
        {
            "BoundingBox" => typeof(BoundingBoxVisualizationSettingsView),
            "Face" => typeof(FaceVisualizationSettingsView),
            "Mesh" => typeof(MeshVisualizationSettingsView),
            "Curve" => typeof(PolylineVisualizationSettingsView),
            "Solid" => typeof(SolidVisualizationSettingsView),
            "Point" => typeof(XyzVisualizationSettingsView),
            _ => null
        };

        if (viewType is null) return;

        if (!_viewCache.TryGetValue(viewType, out var view))
        {
            view = Host.GetService(viewType);
            if (view is not null) _viewCache[viewType] = view;
        }

        ContentArea.Content = view;
    }
}

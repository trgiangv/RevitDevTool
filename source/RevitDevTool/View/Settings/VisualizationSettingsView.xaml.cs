using MahApps.Metro.Controls;
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
        if (HamburgerMenuControl.SelectedItem is HamburgerMenuIconItem item)
        {
            NavigateTo(item.Tag?.ToString());
        }
    }

    private void OnMenuItemInvoked(object sender, HamburgerMenuItemInvokedEventArgs e)
    {
        if (e.InvokedItem is HamburgerMenuIconItem item)
        {
            NavigateTo(item.Tag?.ToString());
        }
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

        HamburgerMenuControl.Content = view;
    }
}

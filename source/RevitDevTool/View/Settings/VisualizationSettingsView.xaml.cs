using MahApps.Metro.Controls;
using RevitDevTool.View.Settings.Visualization;
using System.Windows;

namespace RevitDevTool.View.Settings;

public partial class VisualizationSettingsView
{
    private readonly Dictionary<string, object> _viewCache = new();

    public VisualizationSettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Navigate to first item on load
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

        if (viewType == null) return;

        if (!_viewCache.TryGetValue(tag!, out var view))
        {
            view = Host.GetService(viewType) ?? Activator.CreateInstance(viewType);
            if (view != null)
                _viewCache[tag!] = view;
        }

        HamburgerMenuControl.Content = view;
    }
}

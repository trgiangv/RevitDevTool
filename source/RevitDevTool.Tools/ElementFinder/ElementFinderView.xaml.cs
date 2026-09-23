using System.Windows;
using System.Windows.Controls;
using RevitDevTool.Tools.Helpers;

namespace RevitDevTool.Tools.ElementFinder;

public partial class ElementFinderView
{
    public ElementFinderView()
    {
        InitializeComponent();
        ChromelessToolWindow.Attach(this);
    }

    private void OnCopyMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is null)
            return;

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
        e.Handled = true;
    }
}

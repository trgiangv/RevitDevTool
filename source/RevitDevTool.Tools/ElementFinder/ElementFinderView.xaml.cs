using System.Windows;
using System.Windows.Controls;
using RevitDevTool.Tools.CommandBrowser.Views;
using RevitDevTool.Tools.Helpers;

namespace RevitDevTool.Tools.ElementFinder;

public partial class ElementFinderView
{
    public ElementFinderView()
    {
        InitializeComponent();
        ChromelessToolWindow.Attach(this);
    }

    private void CopyIdentifiersButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is null)
            return;

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }
}

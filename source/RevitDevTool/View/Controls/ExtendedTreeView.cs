using System.Windows;
using TreeView = System.Windows.Controls.TreeView;

namespace RevitDevTool.View.Controls;

public class ExtendedTreeView : TreeView
{
    public ExtendedTreeView()
    {
        SelectedItemChanged += ItemChange;
    }

    private void ItemChange(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (SelectedItem != null)
        {
            SetValue(SelectedTreeItemProperty, SelectedItem);
        }
    }

    public object SelectedTreeItem
    {
        get => GetValue(SelectedTreeItemProperty);
        set => SetValue(SelectedTreeItemProperty, value);
    }

    public static readonly DependencyProperty SelectedTreeItemProperty = DependencyProperty.Register(nameof(SelectedTreeItem), typeof(object), typeof(ExtendedTreeView), new UIPropertyMetadata(null));
}

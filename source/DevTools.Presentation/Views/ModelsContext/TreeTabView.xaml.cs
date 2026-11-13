using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.ViewModels;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DevTools.Presentation.Views.ModelsContext;

public partial class TreeTabView : UserControl
{
    private ModelContextViewModel? _panel;
    private bool _isApplying;

    public TreeTabView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_panel is not null)
        {
            var old = _panel.Tree;
            old.Rows.CollectionChanged -= OnRowsChanging;
            old.Rows.CollectionChanged -= OnRowsChanged;
            old.RowSelectionChanged -= OnVmSelectionChanged;
            old.ScrollRequested -= OnScrollRequested;
        }

        _panel = e.NewValue as ModelContextViewModel;
        if (_panel is null)
        {
            RowList.ItemsSource = null;
            return;
        }

        var tree = _panel.Tree;
        tree.Rows.CollectionChanged += OnRowsChanging;
        RowList.ItemsSource = tree.Rows;
        tree.Rows.CollectionChanged += OnRowsChanged;
        tree.RowSelectionChanged += OnVmSelectionChanged;
        tree.ScrollRequested += OnScrollRequested;
    }

    private void OnRowsChanging(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _isApplying = true;
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _isApplying = false;
        ApplySelection();
    }

    private void OnVmSelectionChanged(object? sender, EventArgs e)
    {
        ApplySelection();
    }

    private void OnScrollRequested(object? sender, TreeRow row)
    {
        RowList.ScrollIntoView(row);
    }

    private void ApplySelection()
    {
        if (_panel is null)
        {
            return;
        }

        var wanted = _panel.Tree.SelectedRows;
        var current = RowList.SelectedItems.Cast<TreeRow>().ToList();
        if (current.Count == wanted.Count && current.All(wanted.Contains))
        {
            return;
        }

        var visible = new HashSet<TreeRow>(_panel.Tree.Rows);
        _isApplying = true;
        try
        {
            RowList.SelectedItems.Clear();
            foreach (var row in wanted.Where(visible.Contains))
            {
                RowList.SelectedItems.Add(row);
            }
        }
        finally
        {
            _isApplying = false;
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplying || _panel is null)
        {
            return;
        }

        _panel.Tree.SetSelectedRows(RowList.SelectedItems.Cast<TreeRow>().ToList());
    }

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (_panel is null)
        {
            return;
        }

        if (RowList.ContextMenu is { } menu)
        {
            menu.ClearValue(ContextMenu.PlacementProperty);
            menu.ClearValue(ContextMenu.HorizontalOffsetProperty);
            menu.ClearValue(ContextMenu.VerticalOffsetProperty);
        }

        _panel.Tree.OpenMenu(RowOf(e.OriginalSource as DependencyObject));
        ApplySelection();
    }

    private void OnRowColorClick(object sender, RoutedEventArgs e)
    {
        if (_panel is not { } panel || sender is not FrameworkElement { DataContext: TreeRow row } button)
        {
            return;
        }

        var host = FindHost(button);
        host?.OpenColorPicker(button, color => panel.Tree.ColorCommand.Execute(new RowColor(row, color)));
    }

    private static ModelContextView? FindHost(DependencyObject? node)
    {
        while (node is not null and not ModelContextView)
        {
            node = VisualTreeHelper.GetParent(node);
        }

        return node as ModelContextView;
    }

    private void OnEyeMouseEnter(object sender, MouseEventArgs e)
    {
        _panel?.Tree.HighlightEyeTargets(((FrameworkElement)sender).DataContext as TreeRow);
    }

    private void OnEyeMouseLeave(object sender, MouseEventArgs e)
    {
        _panel?.Tree.ClearHighlight();
    }

    private void OnRowMenuClick(object sender, RoutedEventArgs e)
    {
        if (_panel is null || RowList.ContextMenu is not { } menu
                           || sender is not FrameworkElement { DataContext: TreeRow row } button)
        {
            return;
        }

        _panel.Tree.OpenMenu(row);
        ApplySelection();
        var corner = button.TranslatePoint(new Point(0, button.ActualHeight), RowList);
        menu.PlacementTarget = RowList;
        menu.Placement = PlacementMode.Relative;
        menu.HorizontalOffset = corner.X;
        menu.VerticalOffset = corner.Y;
        menu.IsOpen = true;
    }

    private async void OnRowsKeyDown(object sender, KeyEventArgs e)
    {
        if (_panel is null)
        {
            return;
        }

        var row = RowList.SelectedItem as TreeRow;
        switch (e.Key)
        {
            case Key.Right when row is { HasChildren: true, IsExpanded: false }:
                e.Handled = true;
                await _panel.Tree.ToggleExpandCommand.ExecuteAsync(row);
                break;
            case Key.Left when row is { IsExpanded: true }:
                e.Handled = true;
                await _panel.Tree.ToggleExpandCommand.ExecuteAsync(row);
                break;
            case Key.Z or Key.B or Key.I or Key.H when Keyboard.Modifiers == ModifierKeys.None:
                e.Handled = true;
                _panel.Tree.OpenMenu(null);
                await _panel.ContextMenu.RunShortcutAsync(e.Key.ToString());
                break;
        }
    }

    private static TreeRow? RowOf(DependencyObject? source)
    {
        while (source is not null and not ListBoxItem)
        {
            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return (source as ListBoxItem)?.DataContext as TreeRow;
    }
}

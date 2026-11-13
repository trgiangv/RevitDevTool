using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.ViewModels;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DevTools.ModelsContext.Views;

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
        var visible = new HashSet<TreeRow>(_panel.Tree.Rows);
        var current = RowList.SelectedItems.Cast<TreeRow>().ToList();
        if (current.Count == wanted.Count && wanted.All(current.Contains))
        {
            return;
        }

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

        _panel.Tree.OpenMenu(RowOf(e.OriginalSource as DependencyObject));
        ApplySelection();
    }

    private void OnRowColorClick(object sender, RoutedEventArgs e)
    {
        if (_panel is null || ((FrameworkElement)sender).DataContext is not TreeRow row)
        {
            return;
        }

        _panel.Tree.OpenMenu(row);
        ApplySelection();
        _ = _panel.ContextMenu[MenuAction.Color].Command.ExecuteAsync(null);
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

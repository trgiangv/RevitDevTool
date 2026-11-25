using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Panel;
using DevTools.Presentation.Views.ModelsContext.Panel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DevTools.Presentation.Views.ModelsContext.Explorer;

public partial class ExplorerTabView : UserControl
{
    private ModelContextViewModel? _panel;
    private bool _isApplying;

    public ExplorerTabView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_panel is not null)
        {
            var old = _panel.Explorer;
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

        var explorer = _panel.Explorer;
        explorer.Rows.CollectionChanged += OnRowsChanging;
        RowList.ItemsSource = explorer.Rows;
        explorer.Rows.CollectionChanged += OnRowsChanged;
        explorer.RowSelectionChanged += OnVmSelectionChanged;
        explorer.ScrollRequested += OnScrollRequested;
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

        var wanted = _panel.Explorer.SelectedRows;
        var current = RowList.SelectedItems.Cast<TreeRow>().ToList();
        if (current.Count == wanted.Count && current.All(wanted.Contains))
        {
            return;
        }

        var visible = new HashSet<TreeRow>(_panel.Explorer.Rows);
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

        _panel.Explorer.SetSelectedRows(RowList.SelectedItems.Cast<TreeRow>().ToList());
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

        _panel.Explorer.OpenMenu(RowOf(e.OriginalSource as DependencyObject));
        ApplySelection();
    }

    private async void OnToolClick(object sender, RoutedEventArgs e)
    {
        if (_panel is not { } panel || sender is not FrameworkElement { DataContext: TreeRow row, Tag: string tag } button || !Enum.TryParse(tag, out MenuAction action))
        {
            return;
        }

        var host = FindHost(button);
        host?.SetColorAnchor(button);
        try
        {
            await panel.Explorer.RunToolAsync(row, action);
        }
        finally
        {
            host?.SetColorAnchor(null);
        }
    }

    private void OnToolShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        RefreshTool(sender);
    }

    private void OnToolHover(object sender, MouseEventArgs e)
    {
        RefreshTool(sender);
    }

    private void RefreshTool(object sender)
    {
        if (_panel is not { } panel || sender is not Button { DataContext: TreeRow row, Tag: string tag } button || !Enum.TryParse(tag, out MenuAction action))
        {
            return;
        }

        var reason = panel.Explorer.ReasonFor(row, action);
        button.IsEnabled = reason is null;
        button.ToolTip = ToolTipOf(row, action, reason);
    }

    private static string ToolTipOf(TreeRow row, MenuAction action, string? reason)
    {
        return action switch
        {
            MenuAction.Color when row.HasColor => "Color: " + row.ColorHex + " · " + (reason ?? "click to change"),
            MenuAction.Halftone when row.IsHalftone => reason is null ? "Halftone: on" : "Halftone: on · " + reason,
            MenuAction.Color => reason ?? "Color",
            _ => reason ?? action.ToString()
        };
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
        _panel?.Explorer.HighlightEyeTargets(((FrameworkElement)sender).DataContext as TreeRow);
    }

    private void OnEyeMouseLeave(object sender, MouseEventArgs e)
    {
        _panel?.Explorer.ClearHighlight();
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
                await _panel.Explorer.ToggleExpandCommand.ExecuteAsync(row);
                break;
            case Key.Left when row is { IsExpanded: true }:
                e.Handled = true;
                await _panel.Explorer.ToggleExpandCommand.ExecuteAsync(row);
                break;
            case Key.Z or Key.B or Key.I or Key.H when e.KeyboardDevice.Modifiers == ModifierKeys.None:
                e.Handled = true;
                _panel.Explorer.OpenMenu(null);
                await _panel.ContextMenu.RunShortcutAsync(e.Key.ToString());
                break;
        }
    }

    private async void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_panel is null || e.ChangedButton != MouseButton.Left || sender is not ListBoxItem { DataContext: TreeRow row }
            || IsInButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        e.Handled = true;
        await _panel.Explorer.OpenRowCommand.ExecuteAsync(row);
    }

    private static bool IsInButton(DependencyObject? source)
    {
        while (source is not null and not ListBoxItem)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }

            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return false;
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

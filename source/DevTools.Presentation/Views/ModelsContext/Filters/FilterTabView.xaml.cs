using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Panel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace DevTools.Presentation.Views.ModelsContext.Filters;

public partial class FilterTabView : UserControl
{
    public static readonly IReadOnlyList<ResultSelectionMode> Modes =
        Enum.GetValues(typeof(ResultSelectionMode)).Cast<ResultSelectionMode>().ToList();

    private ModelContextViewModel? _panel;

    public FilterTabView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _panel = e.NewValue as ModelContextViewModel;
        if (_panel is null)
        {
            return;
        }

        var view = CollectionViewSource.GetDefaultView(_panel.Filter.Library.Sources);
        if (view.GroupDescriptions.Count == 0)
        {
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FilterSourceItem.GroupName)));
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.F2 && _panel is not null && !_panel.Filter.Library.IsEditingName)
        {
            e.Handled = true;
            _panel.Filter.Library.BeginRename();
        }
    }

    private void OnNameVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                NameBox.Focus();
                NameBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void OnNameKeyDown(object sender, KeyEventArgs e)
    {
        if (_panel is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                _panel.Filter.Library.CommitRename();
                break;
            case Key.Escape:
                e.Handled = true;
                _panel.Filter.Library.CancelRename();
                break;
        }
    }

    private void OnNameLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is null)
        {
            return;
        }

        _panel?.Filter.Library.LeaveRename();
    }

    private void OnResultMenuOpening(object sender, ContextMenuEventArgs e)
    {
        _panel?.Filter.OpenResultMenu();
    }

    private void OnResultMenuClick(object sender, RoutedEventArgs e)
    {
        if (_panel is null || ResultRow.ContextMenu is not { } menu)
        {
            return;
        }

        _panel.Filter.OpenResultMenu();
        menu.PlacementTarget = (UIElement)sender;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }
}

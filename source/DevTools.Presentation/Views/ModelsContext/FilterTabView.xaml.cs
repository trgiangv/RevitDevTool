using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace DevTools.Presentation.Views.ModelsContext;

public partial class FilterTabView : UserControl
{
    public static readonly IReadOnlyList<ValueKind> ValueKinds = Enum.GetValues(typeof(ValueKind)).Cast<ValueKind>().ToList();

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

        var view = CollectionViewSource.GetDefaultView(_panel.Filter.Sources);
        if (view.GroupDescriptions.Count == 0)
        {
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FilterSourceItem.GroupName)));
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.F2 && _panel is not null && !_panel.Filter.IsEditingName)
        {
            e.Handled = true;
            _panel.Filter.BeginRename();
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
                _panel.Filter.CommitRename();
                break;
            case Key.Escape:
                e.Handled = true;
                _panel.Filter.CancelRename();
                break;
        }
    }

    private void OnNameLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _panel?.Filter.LeaveRename();
    }

    private void OnValuesOpened(object? sender, EventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RuleViewModel rule })
        {
            _ = rule.LoadValuesCommand.ExecuteAsync(null);
        }
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

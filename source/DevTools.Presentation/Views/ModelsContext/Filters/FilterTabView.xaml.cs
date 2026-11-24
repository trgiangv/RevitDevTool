using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Panel;
using DevTools.Presentation.Views.ModelsContext.Panel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace DevTools.Presentation.Views.ModelsContext.Filters;

public partial class FilterTabView : UserControl
{
    public static readonly IReadOnlyList<ResultSelectionMode> Modes =
        Enum.GetValues(typeof(ResultSelectionMode)).Cast<ResultSelectionMode>().ToList();

    private ModelContextViewModel? _panel;

    public FilterTabView()
    {
        InitializeComponent();
        PausedLine.Text = FilterRunner.SelectionSyncPausedText;
        LayoutUpdated += (_, _) => FitResultLabel();
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

    private void FitResultLabel()
    {
        if (ResultBar.ActualWidth <= 0 || ResultTools.Template?.FindName("ButtonOverflow", ResultTools) is not FrameworkElement overflow)
        {
            return;
        }

        var kept = new FrameworkElement[] { ModeBox, SyncToggle, ResultSeparator, overflow }
                       .Sum(element => element.DesiredSize.Width)
                   + ResultTools.Padding.Left + ResultTools.Padding.Right;
        var label = new FormattedText(ResultLabel.Text, CultureInfo.CurrentCulture, FlowDirection,
            new Typeface(ResultLabel.FontFamily, ResultLabel.FontStyle, ResultLabel.FontWeight, ResultLabel.FontStretch),
            ResultLabel.FontSize, ResultLabel.Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var free = ResultBar.ActualWidth - ResultCount.ActualWidth - CountGroup.Margin.Right - kept;
        var visibility = free >= ResultLabel.Margin.Left + label.WidthIncludingTrailingWhitespace
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (ResultLabel.Visibility == visibility)
        {
            return;
        }

        ResultLabel.Visibility = visibility;
        ResultTools.InvalidateMeasure();
        (ResultTools.Template.FindName("PART_ToolBarPanel", ResultTools) as UIElement)?.InvalidateMeasure();
    }

    private void OnResultRowHover(object sender, MouseEventArgs e)
    {
        foreach (var tool in ResultTools.Items.OfType<Button>())
        {
            RefreshResultTool(tool);
        }
    }

    private void OnResultToolFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        RefreshResultTool(sender);
    }

    private async void OnResultToolClick(object sender, RoutedEventArgs e)
    {
        if (RefreshResultTool(sender) is not { IsEnabled: true } item || _panel is null)
        {
            return;
        }

        var host = FindHost(this);
        host?.SetColorAnchor(ToolBar.GetIsOverflowItem((DependencyObject)sender) ? ResultTools : (FrameworkElement)sender);
        try
        {
            await item.Command.ExecuteAsync(null);
        }
        finally
        {
            host?.SetColorAnchor(null);
        }
    }

    private MenuItemViewModel? RefreshResultTool(object sender)
    {
        if (_panel is null || sender is not Button { Tag: string tag } button || !Enum.TryParse(tag, out MenuAction action))
        {
            return null;
        }

        _panel.Filter.OpenResultMenu();
        var item = _panel.ContextMenu[action];
        button.IsEnabled = item.IsEnabled;
        button.ToolTip = item.ToolTipText ?? item.Text;
        if (action == MenuAction.HideShow)
        {
            var shown = item.IconKey != "Show";
            HandyControl.Controls.IconElement.SetGeometry(button,
                (Geometry)FindResource(shown ? "EyeOpenGeometry" : "EyeCloseGeometry"));
            AutomationProperties.SetName(button, item.Text);
        }

        return item;
    }

    private static ModelContextView? FindHost(DependencyObject? node)
    {
        while (node is not null and not ModelContextView)
        {
            node = VisualTreeHelper.GetParent(node);
        }

        return node as ModelContextView;
    }
}

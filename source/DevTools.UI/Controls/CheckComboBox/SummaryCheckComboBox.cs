using HandyControl.Controls;
using HandyControl.Data;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DevTools.UI.Controls;

[TemplatePart(Name = ElementSelectAll, Type = typeof(CheckComboBoxItem))]
public class SummaryCheckComboBox : ListBox
{
    public const string SummarySeparator = ", ";

    private const string ElementSelectAll = "PART_SelectAll";

    private CheckComboBoxItem? _selectAllItem;

    private bool _isInternalAction;

    public static readonly DependencyProperty MaxDropDownHeightProperty =
        System.Windows.Controls.ComboBox.MaxDropDownHeightProperty.AddOwner(
            typeof(SummaryCheckComboBox),
            new FrameworkPropertyMetadata(SystemParameters.PrimaryScreenHeight / 3));

    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
        nameof(IsDropDownOpen),
        typeof(bool),
        typeof(SummaryCheckComboBox),
        new PropertyMetadata(ValueBoxes.FalseBox, OnIsDropDownOpenChanged, CoerceIsDropDownOpen));

    public static readonly DependencyProperty ShowSelectAllButtonProperty = DependencyProperty.Register(
        nameof(ShowSelectAllButton),
        typeof(bool),
        typeof(SummaryCheckComboBox),
        new PropertyMetadata(ValueBoxes.FalseBox));

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly),
        typeof(bool),
        typeof(SummaryCheckComboBox),
        new PropertyMetadata(ValueBoxes.FalseBox));

    private static readonly DependencyPropertyKey SummaryPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Summary),
        typeof(string),
        typeof(SummaryCheckComboBox),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SummaryProperty = SummaryPropertyKey.DependencyProperty;

    [Bindable(true)]
    [Category("Layout")]
    [TypeConverter(typeof(LengthConverter))]
    public double MaxDropDownHeight
    {
        get => (double)GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, ValueBoxes.BooleanBox(value));
    }

    public bool ShowSelectAllButton
    {
        get => (bool)GetValue(ShowSelectAllButtonProperty);
        set => SetValue(ShowSelectAllButtonProperty, ValueBoxes.BooleanBox(value));
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, ValueBoxes.BooleanBox(value));
    }

    public string Summary => (string)GetValue(SummaryProperty);

    public override void OnApplyTemplate()
    {
        if (_selectAllItem != null)
        {
            _selectAllItem.Selected -= SelectAllItem_Selected;
            _selectAllItem.Unselected -= SelectAllItem_Unselected;
        }

        base.OnApplyTemplate();

        _selectAllItem = GetTemplateChild(ElementSelectAll) as CheckComboBoxItem;
        if (_selectAllItem != null)
        {
            _selectAllItem.Selected += SelectAllItem_Selected;
            _selectAllItem.Unselected += SelectAllItem_Unselected;
        }

        UpdateSelectionState();
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        UpdateSelectionState();
        base.OnSelectionChanged(e);
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        UpdateSelectionState();
    }

    protected override void OnDisplayMemberPathChanged(string oldDisplayMemberPath, string newDisplayMemberPath)
    {
        base.OnDisplayMemberPathChanged(oldDisplayMemberPath, newDisplayMemberPath);
        UpdateSelectionState();
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is CheckComboBoxItem;

    protected override DependencyObject GetContainerForItemOverride() => new CheckComboBoxItem();

    protected override AutomationPeer OnCreateAutomationPeer() => new SummaryCheckComboBoxAutomationPeer(this);

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue)
        {
            ((SummaryCheckComboBox)d).Dispatcher.BeginInvoke(new Action(() => Mouse.Capture(null)), DispatcherPriority.Send);
        }
    }

    private static object CoerceIsDropDownOpen(DependencyObject d, object baseValue)
    {
        return ((SummaryCheckComboBox)d).IsReadOnly ? ValueBoxes.FalseBox : baseValue;
    }

    private void SelectAllItem_Selected(object sender, RoutedEventArgs e) => SwitchAllItems(true);

    private void SelectAllItem_Unselected(object sender, RoutedEventArgs e) => SwitchAllItems(false);

    private void SwitchAllItems(bool selected)
    {
        if (_isInternalAction)
        {
            return;
        }

        _isInternalAction = true;
        var keep = new List<object>();
        foreach (var item in Items)
        {
            if (IsItemEnabled(item) ? selected : SelectedItems.Contains(item))
            {
                keep.Add(item);
            }
        }

        SetSelectedItems(keep);
        _isInternalAction = false;
        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        if (_isInternalAction)
        {
            return;
        }

        var texts = new List<string>(SelectedItems.Count);
        var anyEnabled = false;
        var allEnabledSelected = true;
        foreach (var item in Items)
        {
            var isSelected = SelectedItems.Contains(item);
            if (isSelected)
            {
                texts.Add(DisplayText(item));
            }

            if (IsItemEnabled(item))
            {
                anyEnabled = true;
                allEnabledSelected &= isSelected;
            }
        }

        SetValue(SummaryPropertyKey, string.Join(SummarySeparator, texts));

        if (_selectAllItem != null)
        {
            _isInternalAction = true;
            _selectAllItem.SetCurrentValue(IsSelectedProperty, anyEnabled && allEnabledSelected);
            _isInternalAction = false;
        }
    }

    private bool IsItemEnabled(object item)
    {
        return ItemContainerGenerator.ContainerFromItem(item) is not UIElement { IsEnabled: false };
    }

    private string DisplayText(object? item)
    {
        if (item is ContentControl container)
        {
            item = container.Content;
        }

        var path = DisplayMemberPath;
        if (item is not null && !string.IsNullOrEmpty(path))
        {
            foreach (var name in path.Split('.'))
            {
                item = item is null ? null : TypeDescriptor.GetProperties(item)[name]?.GetValue(item);
            }
        }

        return item?.ToString() ?? string.Empty;
    }

    private sealed class SummaryCheckComboBoxAutomationPeer(SummaryCheckComboBox owner)
        : ListBoxAutomationPeer(owner), IExpandCollapseProvider, IValueProvider
    {
        private SummaryCheckComboBox Box => (SummaryCheckComboBox)Owner;

        public ExpandCollapseState ExpandCollapseState =>
            Box.IsDropDownOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool IsReadOnly => true;

        public string Value => Box.Summary;

        public override object? GetPattern(PatternInterface patternInterface)
        {
            return patternInterface is PatternInterface.ExpandCollapse or PatternInterface.Value
                ? this
                : base.GetPattern(patternInterface);
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;

        public void Expand() => Box.SetCurrentValue(IsDropDownOpenProperty, true);

        public void Collapse() => Box.SetCurrentValue(IsDropDownOpenProperty, false);

        public void SetValue(string value) => throw new ElementNotEnabledException();
    }
}

using HandyControl.Data;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DevTools.UI.Controls;

[TemplatePart(Name = ElementPanel, Type = typeof(TagOverflowPanel))]
[TemplatePart(Name = ElementSearch, Type = typeof(HandyControl.Controls.SearchBar))]
[TemplatePart(Name = ElementSelectAll, Type = typeof(CheckBox))]
[TemplatePart(Name = ElementPopup, Type = typeof(Popup))]
[TemplatePart(Name = ElementItemsPresenter, Type = typeof(ItemsPresenter))]
public class TagCheckComboBox : ListBox
{
    public const string SearchIdSuffix = "Search";
    public const string SelectAllIdSuffix = "SelectAll";
    public const string MoreIdSuffix = "More";
    public const string TagIdSuffix = "Tag";
    public const string TagRemoveIdSuffix = "TagRemove";

    private const string ElementPanel = "PART_Panel";
    private const string ElementSearch = "PART_Search";
    private const string ElementSelectAll = "PART_SelectAll";
    private const string ElementPopup = "PART_Popup";
    private const string ElementItemsPresenter = "ItemsPresenter";
    private const string DefaultIdPrefix = nameof(TagCheckComboBox);

    public static readonly DependencyProperty MaxDropDownHeightProperty =
        System.Windows.Controls.ComboBox.MaxDropDownHeightProperty.AddOwner(
            typeof(TagCheckComboBox),
            new FrameworkPropertyMetadata(SystemParameters.PrimaryScreenHeight / 3));

    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
        nameof(IsDropDownOpen),
        typeof(bool),
        typeof(TagCheckComboBox),
        new FrameworkPropertyMetadata(ValueBoxes.FalseBox, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsDropDownOpenChanged));

    public static readonly DependencyProperty ShowSelectAllButtonProperty = DependencyProperty.Register(
        nameof(ShowSelectAllButton),
        typeof(bool),
        typeof(TagCheckComboBox),
        new PropertyMetadata(ValueBoxes.FalseBox));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder),
        typeof(string),
        typeof(TagCheckComboBox),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SearchPlaceholderProperty = DependencyProperty.Register(
        nameof(SearchPlaceholder),
        typeof(string),
        typeof(TagCheckComboBox),
        new PropertyMetadata("Search"));

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText),
        typeof(string),
        typeof(TagCheckComboBox),
        new PropertyMetadata("No matches"));

    public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
        nameof(SearchText),
        typeof(string),
        typeof(TagCheckComboBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSearchTextChanged));

    public static readonly DependencyProperty TagStyleProperty = DependencyProperty.Register(
        nameof(TagStyle),
        typeof(Style),
        typeof(TagCheckComboBox),
        new PropertyMetadata(null, OnTagStyleChanged));

    public static readonly DependencyProperty TagSpacingProperty = DependencyProperty.Register(
        nameof(TagSpacing),
        typeof(double),
        typeof(TagCheckComboBox),
        new PropertyMetadata(4d));

    private static readonly DependencyPropertyKey HasSelectedItemsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasSelectedItems),
        typeof(bool),
        typeof(TagCheckComboBox),
        new PropertyMetadata(ValueBoxes.FalseBox));

    public static readonly DependencyProperty HasSelectedItemsProperty = HasSelectedItemsPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey HasFilterMatchesPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasFilterMatches),
        typeof(bool),
        typeof(TagCheckComboBox),
        new PropertyMetadata(ValueBoxes.FalseBox));

    public static readonly DependencyProperty HasFilterMatchesProperty = HasFilterMatchesPropertyKey.DependencyProperty;

    private TagOverflowPanel? _panel;
    private TextBox? _search;
    private ToggleButton? _selectAll;
    private Popup? _popup;
    private TagCheckComboBoxTag? _overflow;
    private TagCheckComboBoxItem? _probe;
    private bool _isInternalAction;
    private const int PressCloseWindow = 500;

    private bool _isPressed;
    private bool _closesOnRelease;
    private int _pressClosedAt = int.MinValue;
    private bool _isRemovingTag;

    static TagCheckComboBox()
    {
        CommandManager.RegisterClassCommandBinding(
            typeof(TagCheckComboBox),
            new CommandBinding(ApplicationCommands.SelectAll, OnSelectAllCommand));
    }

    public TagCheckComboBox()
    {
        AddHandler(HandyControl.Controls.Tag.ClosedEvent, new RoutedEventHandler(OnTagClosed));
    }

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

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string SearchPlaceholder
    {
        get => (string)GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public Style? TagStyle
    {
        get => (Style?)GetValue(TagStyleProperty);
        set => SetValue(TagStyleProperty, value);
    }

    public double TagSpacing
    {
        get => (double)GetValue(TagSpacingProperty);
        set => SetValue(TagSpacingProperty, value);
    }

    public bool HasSelectedItems => (bool)GetValue(HasSelectedItemsProperty);

    public bool HasFilterMatches => (bool)GetValue(HasFilterMatchesProperty);

    public int HiddenTagCount => _panel?.HiddenCount ?? 0;

    public string PartAutomationId(string suffix)
    {
        var owner = AutomationProperties.GetAutomationId(this);
        return (string.IsNullOrEmpty(owner) ? DefaultIdPrefix : owner) + "." + suffix;
    }

    public string DisplayText(object? item)
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

    public bool IsItemEnabled(object item)
    {
        if (ItemContainerGenerator.ContainerFromItem(item) is UIElement container)
        {
            return container.IsEnabled;
        }

        if (item is UIElement element)
        {
            return element.IsEnabled;
        }

        return ProbeOf(item).IsEnabled;
    }

    private TagCheckComboBoxItem ProbeOf(object item)
    {
        _probe ??= new TagCheckComboBoxItem();
        var style = ItemContainerStyle ?? ItemContainerStyleSelector?.SelectStyle(item, _probe);
        if (!ReferenceEquals(_probe.Style, style))
        {
            _probe.Style = style;
        }

        _probe.DataContext = item;
        _probe.Content = item;
        return _probe;
    }

    public bool IsItemMatch(object item)
    {
        var search = SearchText;
        return string.IsNullOrEmpty(search) || DisplayText(item).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public void ToggleItem(object item)
    {
        if (!IsItemEnabled(item))
        {
            return;
        }

        if (SelectedItems.Contains(item))
        {
            SelectedItems.Remove(item);
        }
        else
        {
            SelectedItems.Add(item);
        }
    }

    internal object? ItemPattern(object item, PatternInterface pattern)
    {
        return UIElementAutomationPeer.CreatePeerForElement(this) is TagCheckComboBoxAutomationPeer peer
            ? new TagCheckComboBoxItemAutomationPeer(item, peer).GetPattern(pattern)
            : null;
    }

    public void SetFilteredSelection(bool selected)
    {
        var current = SelectedSet();
        var keep = new List<object>();
        foreach (var item in Items)
        {
            var isTarget = IsItemMatch(item) && IsItemEnabled(item);
            if (isTarget ? selected : current.Contains(item))
            {
                keep.Add(item);
            }
        }

        _isInternalAction = true;
        try
        {
            SetSelectedItems(keep);
        }
        finally
        {
            _isInternalAction = false;
        }

        RefreshTags();
        RefreshSelectAll();
    }

    public override void OnApplyTemplate()
    {
        if (_selectAll is not null)
        {
            _selectAll.Checked -= OnSelectAllChecked;
            _selectAll.Unchecked -= OnSelectAllUnchecked;
        }

        if (_panel is not null)
        {
            _panel.Children.Clear();
            _panel.Overflow = null;
        }

        base.OnApplyTemplate();

        _panel = GetTemplateChild(ElementPanel) as TagOverflowPanel;
        _search = GetTemplateChild(ElementSearch) as TextBox;
        _selectAll = GetTemplateChild(ElementSelectAll) as ToggleButton;
        _popup = GetTemplateChild(ElementPopup) as Popup;
        (GetTemplateChild(ElementItemsPresenter) as ItemsPresenter)?.ApplyTemplate();

        if (_search is not null)
        {
            AutomationProperties.SetAutomationId(_search, PartAutomationId(SearchIdSuffix));
        }

        if (_selectAll is not null)
        {
            AutomationProperties.SetAutomationId(_selectAll, PartAutomationId(SelectAllIdSuffix));
            _selectAll.Checked += OnSelectAllChecked;
            _selectAll.Unchecked += OnSelectAllUnchecked;
        }

        if (_panel is not null)
        {
            _overflow = new TagCheckComboBoxTag { Style = TagStyle, ShowCloseButton = false, Focusable = false };
            AutomationProperties.SetAutomationId(_overflow, PartAutomationId(MoreIdSuffix));
            _panel.Overflow = _overflow;
        }

        RefreshTags();
        RefreshFilter();
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        if (!_isInternalAction)
        {
            RefreshTags();
            RefreshSelectAll();
        }

        base.OnSelectionChanged(e);
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        RefreshTags();
        RefreshFilter();
    }

    protected override void OnDisplayMemberPathChanged(string oldDisplayMemberPath, string newDisplayMemberPath)
    {
        base.OnDisplayMemberPathChanged(oldDisplayMemberPath, newDisplayMemberPath);
        RefreshTags();
        RefreshFilter();
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is TagCheckComboBoxItem;

    protected override DependencyObject GetContainerForItemOverride() => new TagCheckComboBoxItem();

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        element.SetCurrentValue(VisibilityProperty, IsItemMatch(item) ? Visibility.Visible : Visibility.Collapsed);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TagCheckComboBoxAutomationPeer(this);

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (IsDropDownOpen && TagUnderRemove(e) is { } tag)
        {
            _isRemovingTag = true;
            if (IsItemEnabled(tag.Tag))
            {
                SelectedItems.Remove(tag.Tag);
            }

            e.Handled = true;
            return;
        }

        base.OnPreviewMouseLeftButtonDown(e);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_isRemovingTag)
        {
            _isRemovingTag = false;
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseLeftButtonUp(e);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _isPressed = false;
        if (e.Handled || !IsEnabled || !IsFieldSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        _closesOnRelease = IsDropDownOpen || unchecked(Environment.TickCount - _pressClosedAt) is >= 0 and < PressCloseWindow;
        Focus();
        _isPressed = true;
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_isPressed)
        {
            return;
        }

        _isPressed = false;
        if (!e.Handled && IsEnabled && IsFieldSource(e.OriginalSource as DependencyObject))
        {
            SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.BooleanBox(!_closesOnRelease));
            e.Handled = true;
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _isPressed = false;
        base.OnMouseLeave(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (!IsDropDownOpen)
        {
            if (key is Key.Down or Key.F4 or Key.Enter or Key.Space)
            {
                SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.TrueBox);
                e.Handled = true;
            }
        }
        else
        {
            var inSearch = _search is { IsKeyboardFocusWithin: true };
            switch (key)
            {
                case Key.Escape:
                    SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.FalseBox);
                    Focus();
                    e.Handled = true;
                    break;
                case Key.Down:
                    MoveItemFocus(1);
                    e.Handled = true;
                    break;
                case Key.Up:
                    MoveItemFocus(-1);
                    e.Handled = true;
                    break;
                case Key.Space or Key.Enter when !inSearch:
                    ToggleFocusedItem();
                    e.Handled = true;
                    break;
            }
        }

        if (!e.Handled)
        {
            base.OnPreviewKeyDown(e);
        }
    }

    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        if (_search is not null && !_search.IsKeyboardFocusWithin && !string.IsNullOrEmpty(e.Text) && !char.IsControl(e.Text[0]))
        {
            SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.TrueBox);
            SetCurrentValue(SearchTextProperty, SearchText + e.Text);
            FocusSearch();
            e.Handled = true;
            return;
        }

        base.OnPreviewTextInput(e);
    }

    private static void OnSelectAllCommand(object sender, ExecutedRoutedEventArgs e)
    {
        ((TagCheckComboBox)sender).SetFilteredSelection(true);
        e.Handled = true;
    }

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (TagCheckComboBox)d;
        var isOpen = (bool)e.NewValue;
        if (isOpen)
        {
            box.Dispatcher.BeginInvoke(new Action(box.FocusSearch), DispatcherPriority.Input);
        }
        else
        {
            if (box._popup?.Child is { IsKeyboardFocusWithin: true })
            {
                box.Focus();
            }

            if (Mouse.LeftButton == MouseButtonState.Pressed && box.InputHitTest(Mouse.GetPosition(box)) is not null)
            {
                box._pressClosedAt = Environment.TickCount;
            }

            box.SetCurrentValue(SearchTextProperty, string.Empty);
            box.Dispatcher.BeginInvoke(new Action(() => Mouse.Capture(null)), DispatcherPriority.Send);
        }

        if (UIElementAutomationPeer.FromElement(box) is TagCheckComboBoxAutomationPeer peer)
        {
            peer.ResetChildrenCache();
            peer.RaisePropertyChangedEvent(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                isOpen ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                isOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
        }
    }

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TagCheckComboBox)d).RefreshFilter();
    }

    private static void OnTagStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (TagCheckComboBox)d;
        if (box._overflow is not null)
        {
            box._overflow.Style = (Style?)e.NewValue;
        }

        box.RefreshTags();
    }

    private void OnTagClosed(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TagCheckComboBoxTag { Tag: { } item } tag && !ReferenceEquals(tag, _overflow) && IsItemEnabled(item))
        {
            SelectedItems.Remove(item);
        }

        e.Handled = true;
    }

    private void OnSelectAllChecked(object sender, RoutedEventArgs e)
    {
        if (!_isInternalAction)
        {
            SetFilteredSelection(true);
        }
    }

    private void OnSelectAllUnchecked(object sender, RoutedEventArgs e)
    {
        if (!_isInternalAction)
        {
            SetFilteredSelection(false);
        }
    }

    private HashSet<object> SelectedSet()
    {
        var set = new HashSet<object>();
        foreach (var item in SelectedItems)
        {
            set.Add(item);
        }

        return set;
    }

    private void RefreshTags()
    {
        SetValue(HasSelectedItemsPropertyKey, ValueBoxes.BooleanBox(SelectedItems.Count > 0));
        if (_panel is null)
        {
            return;
        }

        foreach (var child in _panel.Children.OfType<TagCheckComboBoxTag>().Where(child => !ReferenceEquals(child, _overflow)).ToList())
        {
            _panel.Children.Remove(child);
        }

        var tagId = PartAutomationId(TagIdSuffix);
        var index = 0;
        foreach (var item in SelectedItems)
        {
            var label = DisplayText(item);
            var tag = new TagCheckComboBoxTag
            {
                Style = TagStyle,
                Tag = item,
                Content = label,
                ToolTip = label,
                Focusable = false,
                ShowCloseButton = IsItemEnabled(item),
            };
            AutomationProperties.SetAutomationId(tag, tagId);
            AutomationProperties.SetName(tag, label);
            _panel.Children.Insert(index++, tag);
        }

        _panel.InvalidateMeasure();
    }

    private void RefreshFilter()
    {
        var any = false;
        for (var index = 0; index < Items.Count; index++)
        {
            var item = Items[index];
            var match = IsItemMatch(item);
            any |= match;
            if (ItemContainerGenerator.ContainerFromIndex(index) is UIElement container)
            {
                container.SetCurrentValue(VisibilityProperty, match ? Visibility.Visible : Visibility.Collapsed);
            }
        }

        SetValue(HasFilterMatchesPropertyKey, ValueBoxes.BooleanBox(any));
        RefreshSelectAll();
    }

    private void RefreshSelectAll()
    {
        if (_selectAll is null)
        {
            return;
        }

        var selected = SelectedSet();
        var any = false;
        var all = true;
        foreach (var item in Items)
        {
            if (IsItemMatch(item) && IsItemEnabled(item))
            {
                any = true;
                all &= selected.Contains(item);
            }
        }

        _isInternalAction = true;
        try
        {
            _selectAll.SetCurrentValue(ToggleButton.IsCheckedProperty, ValueBoxes.BooleanBox(any && all));
            _selectAll.SetCurrentValue(IsEnabledProperty, ValueBoxes.BooleanBox(any));
        }
        finally
        {
            _isInternalAction = false;
        }
    }

    private bool IsFieldSource(DependencyObject? source)
    {
        while (source is not null && !ReferenceEquals(source, this))
        {
            if (source is ButtonBase)
            {
                return false;
            }

            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }

        return source is not null;
    }

    private TagCheckComboBoxTag? TagUnderRemove(MouseEventArgs e)
    {
        if (_panel is null || _popup?.Child is UIElement child && child.InputHitTest(e.GetPosition(child)) is not null)
        {
            return null;
        }

        var source = _panel.InputHitTest(e.GetPosition(_panel)) as DependencyObject;
        var isRemove = false;
        while (source is not null && !ReferenceEquals(source, _panel))
        {
            isRemove |= source is ButtonBase;
            if (source is TagCheckComboBoxTag tag)
            {
                return isRemove && !ReferenceEquals(tag, _overflow) && tag.Tag is not null ? tag : null;
            }

            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }

        return null;
    }

    private void FocusSearch()
    {
        if (_search is null || !IsDropDownOpen)
        {
            return;
        }

        _search.Focus();
        _search.CaretIndex = _search.Text.Length;
    }

    private int FocusedIndex()
    {
        if (Keyboard.FocusedElement is not DependencyObject focused || ContainerFromElement(focused) is not { } container)
        {
            return -1;
        }

        return ItemContainerGenerator.IndexFromContainer(container);
    }

    private void MoveItemFocus(int direction)
    {
        var index = FocusedIndex();
        if (index < 0 && direction < 0)
        {
            return;
        }

        for (var next = index + direction; next >= 0 && next < Items.Count; next += direction)
        {
            var item = Items[next];
            if (IsItemMatch(item) && IsItemEnabled(item))
            {
                FocusIndex(next);
                return;
            }
        }

        if (direction < 0)
        {
            FocusSearch();
        }
    }

    private void FocusIndex(int index)
    {
        if (ItemContainerGenerator.ContainerFromIndex(index) is null)
        {
            ScrollIntoView(Items[index]);
            UpdateLayout();
        }

        if (ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem container)
        {
            container.BringIntoView();
            container.Focus();
        }
    }

    private void ToggleFocusedItem()
    {
        var index = FocusedIndex();
        if (index >= 0)
        {
            ToggleItem(Items[index]);
        }
    }

    private void SelectOnly(object item)
    {
        var keep = new List<object> { item };
        foreach (var selected in SelectedItems)
        {
            if (!ReferenceEquals(selected, item) && !IsItemEnabled(selected))
            {
                keep.Add(selected);
            }
        }

        SetSelectedItems(keep);
    }

    private sealed class TagCheckComboBoxAutomationPeer(TagCheckComboBox owner)
        : ListBoxAutomationPeer(owner), IExpandCollapseProvider, IValueProvider
    {
        private readonly Dictionary<object, ItemAutomationPeer> _virtualPeers = [];

        private TagCheckComboBox Box => (TagCheckComboBox)Owner;

        public ExpandCollapseState ExpandCollapseState =>
            Box.IsDropDownOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool IsReadOnly => true;

        public string Value => string.Join(", ", Box.SelectedItems.Cast<object>().Select(Box.DisplayText));

        public override object? GetPattern(PatternInterface patternInterface)
        {
            return patternInterface is PatternInterface.ExpandCollapse or PatternInterface.Value
                ? this
                : base.GetPattern(patternInterface);
        }

        public void Expand() => Box.SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.TrueBox);

        public void Collapse() => Box.SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.FalseBox);

        public void SetValue(string value) => throw new ElementNotEnabledException();

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;

        protected override string GetClassNameCore() => nameof(TagCheckComboBox);

        protected override ItemAutomationPeer CreateItemAutomationPeer(object item) => new TagCheckComboBoxItemAutomationPeer(item, this);

        protected override List<AutomationPeer>? GetChildrenCore()
        {
            var children = new List<AutomationPeer>();
            AddPart(children, Box._panel);
            if (Box.IsDropDownOpen)
            {
                AddPart(children, Box._search);
                if (Box._selectAll is { IsVisible: true })
                {
                    AddPart(children, Box._selectAll);
                }
            }

            var realized = new Dictionary<object, AutomationPeer>();
            foreach (var peer in Box.IsDropDownOpen ? base.GetChildrenCore() ?? [] : [])
            {
                if (peer is ItemAutomationPeer { Item: { } item } && !realized.ContainsKey(item))
                {
                    realized.Add(item, peer);
                }
            }

            var previous = new Dictionary<object, ItemAutomationPeer>(_virtualPeers);
            _virtualPeers.Clear();
            foreach (var item in Box.Items)
            {
                if (realized.TryGetValue(item, out var peer))
                {
                    children.Add(peer);
                }
                else if (!_virtualPeers.ContainsKey(item))
                {
                    var virtualPeer = previous.TryGetValue(item, out var kept) ? kept : CreateItemAutomationPeer(item);
                    _virtualPeers.Add(item, virtualPeer);
                    children.Add(virtualPeer);
                }
            }

            return children.Count == 0 ? null : children;
        }

        private static void AddPart(List<AutomationPeer> children, UIElement? element)
        {
            if (element is null)
            {
                return;
            }

            if (element is Panel panel)
            {
                foreach (UIElement child in panel.Children)
                {
                    if (panel is not TagOverflowPanel tags || tags.IsShown(child))
                    {
                        AddPart(children, child);
                    }
                }

                return;
            }

            if (CreatePeerForElement(element) is { } peer)
            {
                children.Add(peer);
            }
        }
    }

    private sealed class TagCheckComboBoxItemAutomationPeer(object item, SelectorAutomationPeer owner)
        : ListBoxItemAutomationPeer(item, owner), ISelectionItemProvider, IToggleProvider
    {
        private TagCheckComboBox Box => (TagCheckComboBox)ItemsControlAutomationPeer.Owner;

        public ToggleState ToggleState => Box.SelectedItems.Contains(Item) ? ToggleState.On : ToggleState.Off;

        bool ISelectionItemProvider.IsSelected => Box.SelectedItems.Contains(Item);

        IRawElementProviderSimple ISelectionItemProvider.SelectionContainer => ProviderFromPeer(ItemsControlAutomationPeer);

        public override object? GetPattern(PatternInterface patternInterface)
        {
            return patternInterface is PatternInterface.Toggle or PatternInterface.SelectionItem
                ? this
                : base.GetPattern(patternInterface);
        }

        public void Toggle()
        {
            EnsureEnabled();
            Box.ToggleItem(Item);
        }

        void ISelectionItemProvider.AddToSelection()
        {
            EnsureEnabled();
            if (!Box.SelectedItems.Contains(Item))
            {
                Box.SelectedItems.Add(Item);
            }
        }

        void ISelectionItemProvider.RemoveFromSelection()
        {
            EnsureEnabled();
            Box.SelectedItems.Remove(Item);
        }

        void ISelectionItemProvider.Select()
        {
            EnsureEnabled();
            Box.SelectOnly(Item);
        }

        protected override bool IsEnabledCore() => Box.IsEnabled && Box.IsItemEnabled(Item);

        protected override string GetAutomationIdCore() =>
            Box.IsDropDownOpen ? base.GetAutomationIdCore() : AutomationProperties.GetAutomationId(Box.ProbeOf(Item));

        protected override string GetNameCore()
        {
            if (Box.IsDropDownOpen)
            {
                return base.GetNameCore();
            }

            var name = AutomationProperties.GetName(Box.ProbeOf(Item));
            return string.IsNullOrEmpty(name) ? Box.DisplayText(Item) : name;
        }

        protected override bool IsOffscreenCore() => !Box.IsDropDownOpen || base.IsOffscreenCore();

        protected override bool IsControlElementCore() => !Box.IsDropDownOpen || base.IsControlElementCore();

        protected override bool IsContentElementCore() => !Box.IsDropDownOpen || base.IsContentElementCore();

        protected override Rect GetBoundingRectangleCore() => Box.IsDropDownOpen ? base.GetBoundingRectangleCore() : Rect.Empty;

        protected override List<AutomationPeer>? GetChildrenCore() => Box.IsDropDownOpen ? base.GetChildrenCore() : null;

        private void EnsureEnabled()
        {
            if (!IsEnabledCore())
            {
                throw new ElementNotEnabledException();
            }
        }
    }
}

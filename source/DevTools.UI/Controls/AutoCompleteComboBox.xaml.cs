using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Binding = System.Windows.Data.Binding;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

namespace DevTools.UI.Controls;

/// <summary>
/// ComboBox with autocomplete/filtering support.
/// Ported from DotNetKit.Wpf.AutoCompleteComboBox.
/// </summary>
public partial class AutoCompleteComboBox
{
    private DispatcherTimer? _debounceTimer;
    private Predicate<object>? _defaultItemsFilter;
    private string? _previousText;
    private static readonly char[] BindingPathMarks = ['(', '[', '/'];
    private BindingEvaluator<string>? _itemTextEvaluator;
    private ConditionalWeakTable<object, string> _itemTexts = new();
    private string? _itemTextPath;
    private readonly Dictionary<(Type, string), PropertyDescriptor?> _textProperties = [];
    private string? _cachedFilterQuery;
    private Predicate<object>? _cachedFilter;
    private bool _isApplyingOwnView;
    private FilteredItems? _ownItems;
    private ControlTemplate? _savedErrorTemplate;

    [UsedImplicitly]
    public TextBox? EditableTextBox
    {
        get
        {
            field ??= FindDescendant(this, "PART_EditableTextBox") as TextBox;
            return field;
        }
    }

    private string GetItemText(object? item)
    {
        if (item is null) return string.Empty;

        var path = TextSearch.GetTextPath(this) ?? string.Empty;
        if (!string.Equals(path, _itemTextPath, StringComparison.Ordinal))
        {
            _itemTextPath = path;
            _itemTexts = new ConditionalWeakTable<object, string>();
            _textProperties.Clear();
        }

        if (_itemTexts.TryGetValue(item, out var cached)) return cached;

        var text = ReadItemText(item, path);
        if (!item.GetType().IsValueType)
        {
            _itemTexts.Add(item, text);
        }

        return text;
    }

    private string ReadItemText(object item, string path)
    {
        if (path.Length == 0) return item.ToString() ?? string.Empty;

        if (path.IndexOfAny(BindingPathMarks) >= 0)
        {
            _itemTextEvaluator ??= new BindingEvaluator<string>();
            _itemTextEvaluator.SetBinding(item, path);
            return _itemTextEvaluator.Value ?? string.Empty;
        }

        object? current = item;
        foreach (var name in path.Split('.'))
        {
            if (current is null) break;
            var type = current.GetType();
            if (!_textProperties.TryGetValue((type, name), out var property))
            {
                property = TypeDescriptor.GetProperties(type)[name];
                _textProperties[(type, name)] = property;
            }

            current = property?.GetValue(current);
        }

        return current?.ToString() ?? string.Empty;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_EditableTextBox") is TextBox textBox)
        {
            textBox.SetBinding(TextBoxBase.CaretBrushProperty, new Binding(nameof(Foreground)) { Source = this });
        }
    }

    protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
    {
        if (!_isApplyingOwnView && newValue is IList list and not ICollectionView)
        {
            ApplyOwnItems(new FilteredItems(list));
            return;
        }

        base.OnItemsSourceChanged(oldValue, newValue);
        if (!ReferenceEquals(newValue, _ownItems))
        {
            _ownItems = null;
        }

        _defaultItemsFilter = newValue is ICollectionView cv ? cv.Filter : null;
        _itemTexts = new ConditionalWeakTable<object, string>();
        _cachedFilterQuery = null;
        _cachedFilter = null;
    }

    private void ApplyOwnItems(FilteredItems items)
    {
        _ownItems = items;
        _isApplyingOwnView = true;
        try
        {
            SetCurrentValue(ItemsSourceProperty, items);
        }
        finally
        {
            _isApplyingOwnView = false;
        }
    }

    private void ApplyFilter(Predicate<object>? filter, string? exact = null)
    {
        if (_ownItems is { } items)
        {
            items.Apply(filter, string.IsNullOrEmpty(exact)
                ? null
                : item => string.Equals(GetItemText(item), exact, StringComparison.OrdinalIgnoreCase));
            return;
        }

        using (Items.DeferRefresh())
        {
            Items.Filter = filter;
        }
    }

    #region Grouping

    public static readonly DependencyProperty GroupPathProperty = DependencyProperty.Register(
        nameof(GroupPath), typeof(string), typeof(AutoCompleteComboBox),
        new PropertyMetadata(null, (d, _) => ((AutoCompleteComboBox)d).ApplyGroups()));

    public string? GroupPath
    {
        get => (string?)GetValue(GroupPathProperty);
        set => SetValue(GroupPathProperty, value);
    }

    private void ApplyGroups()
    {
        Items.GroupDescriptions.Clear();
        if (string.IsNullOrEmpty(GroupPath))
        {
            return;
        }

        var groups = new PropertyGroupDescription(GroupPath);
        groups.SortDescriptions.Add(new SortDescription(nameof(CollectionViewGroup.Name), ListSortDirection.Ascending));
        Items.GroupDescriptions.Add(groups);
    }

    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
    {
        return new AutoCompleteComboBoxAutomationPeer(this);
    }

    #endregion

    #region Setting

    [UsedImplicitly]
    public static DependencyProperty SettingProperty { get; } =
        DependencyProperty.Register(nameof(Setting), typeof(AutoCompleteComboBoxSetting), typeof(AutoCompleteComboBox));

    public AutoCompleteComboBoxSetting? Setting
    {
        get => (AutoCompleteComboBoxSetting?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    private AutoCompleteComboBoxSetting SettingOrDefault => Setting ?? AutoCompleteComboBoxSetting.Default;

    #endregion

    #region TextChanged / Filtering

    private readonly struct TextBoxStateSaver(TextBox? textBox) : IDisposable
    {
        private readonly int _selectionStart = textBox?.SelectionStart ?? 0;
        private readonly int _selectionLength = textBox?.SelectionLength ?? 0;
        private readonly string _text = textBox?.Text ?? "";

        public void Dispose()
        {
            if (textBox is null) return;
            textBox.Text = _text;
            textBox.Select(_selectionStart, _selectionLength);
        }
    }

    private void UpdateFilter()
    {
        var filter = GetFilter();
        var textBox = EditableTextBox;

        using (new TextBoxStateSaver(textBox))
        {
            ApplyFilter(filter, Text?.Trim());
        }

        textBox?.Select(textBox.SelectionStart + textBox.SelectionLength, 0);
    }

    private void UpdateSuggestionList(bool controlOpen)
    {
        var text = Text;
        if (text == _previousText) return;
        _previousText = text;

        if (string.IsNullOrEmpty(text))
        {
            ClearSuggestions(controlOpen);
            return;
        }

        if (SelectedItem is not null && GetItemText(SelectedItem) == text)
        {
            return;
        }

        using (new TextBoxStateSaver(EditableTextBox))
        {
            SelectedItem = null;
        }

        UpdateFilter();
        if (controlOpen && ShouldOpenSuggestions())
        {
            IsDropDownOpen = true;
        }
    }

    private void ClearSuggestions(bool controlOpen)
    {
        if (controlOpen) IsDropDownOpen = false;
        SelectedItem = null;
        ApplyFilter(_defaultItemsFilter);
        _cachedFilterQuery = null;
        _cachedFilter = null;
    }

    private bool ShouldOpenSuggestions()
    {
        return !IsDropDownOpen && IsKeyboardFocusWithin && Items.Count > 0
               && Items.Count <= SettingOrDefault.MaxSuggestionCount;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var setting = SettingOrDefault;

        if (setting.Delay <= TimeSpan.Zero)
        {
            UpdateSuggestionList(controlOpen: true);
            return;
        }

        if (_debounceTimer is null)
        {
            _debounceTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = setting.Delay };
            _debounceTimer.Tick += (_, _) =>
            {
                _debounceTimer.Stop();
                UpdateSuggestionList(controlOpen: true);
            };
        }

        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    #endregion

    protected override void OnDropDownOpened(EventArgs e)
    {
        base.OnDropDownOpened(e);

        if (Validation.GetHasError(this) && _savedErrorTemplate is null)
        {
            _savedErrorTemplate = Validation.GetErrorTemplate(this);
            Validation.SetErrorTemplate(this, null);
        }

        _debounceTimer?.Stop();

        UpdateSuggestionList(controlOpen: false);

        var textBox = EditableTextBox;
        if (textBox is not null)
            textBox.Select(textBox.SelectionStart + textBox.SelectionLength, 0);
    }

    protected override void OnDropDownClosed(EventArgs e)
    {
        base.OnDropDownClosed(e);
        if (_savedErrorTemplate is not null)
        {
            Validation.SetErrorTemplate(this, _savedErrorTemplate);
            _savedErrorTemplate = null;
        }
    }

    private void ComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control) || e.Key != System.Windows.Input.Key.Space) return;
        e.Handled = true;
        IsDropDownOpen = true;
    }

    private Predicate<object> GetFilter()
    {
        var query = Text ?? "";
        if (query == _cachedFilterQuery && _cachedFilter is not null)
        {
            return _cachedFilter;
        }

        var filter = SettingOrDefault.GetFilter(query, GetItemText);
        var combined = _defaultItemsFilter is not null
            ? i => _defaultItemsFilter(i) && filter(i)
            : filter;

        _cachedFilterQuery = query;
        _cachedFilter = combined;
        return combined;
    }

    public AutoCompleteComboBox()
    {
        InitializeComponent();
        AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnTextChanged));
    }

    #region Helpers

    private sealed class BindingEvaluator<T> : DependencyObject
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(T), typeof(BindingEvaluator<T>));

        public T? Value
        {
            get => (T?)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        private void SetBinding(Binding binding)
        {
            BindingOperations.SetBinding(this, ValueProperty, binding);
        }

        public void SetBinding(object dataContext, string propertyPath)
        {
            SetBinding(new Binding(propertyPath) { Source = dataContext });
        }
    }

    private static FrameworkElement? FindDescendant(DependencyObject obj, string childName)
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(obj);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var childCount = VisualTreeHelper.GetChildrenCount(current);

            for (var i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(current, i);
                if (child is FrameworkElement fe && fe.Name == childName)
                    return fe;
                queue.Enqueue(child);
            }
        }

        return null;
    }

    #endregion
}

public sealed class AutoCompleteComboBoxAutomationPeer(AutoCompleteComboBox owner)
    : System.Windows.Automation.Peers.ComboBoxAutomationPeer(owner)
{
    protected override List<System.Windows.Automation.Peers.AutomationPeer>? GetChildrenCore()
    {
        var children = base.GetChildrenCore();
        if (children is null || !children.OfType<System.Windows.Automation.Peers.GroupItemAutomationPeer>().Any())
        {
            return children;
        }

        var flat = new List<System.Windows.Automation.Peers.AutomationPeer>();
        foreach (var child in children)
        {
            if (child is not System.Windows.Automation.Peers.GroupItemAutomationPeer { Owner: GroupItem group } peer)
            {
                flat.Add(child);
                continue;
            }

            if (HeaderOf(group) is { } header
                && System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(header) is { } headerPeer)
            {
                flat.Add(headerPeer);
            }

            flat.AddRange(peer.GetChildren() ?? []);
        }

        return flat;
    }

    private static StickyGroupHeader? HeaderOf(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is StickyGroupHeader header)
            {
                return header;
            }

            if (child is not ItemsPresenter && HeaderOf(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}

/// <summary>
/// Configuration for <see cref="AutoCompleteComboBox"/> filtering and dropdown behavior.
/// Ported from DotNetKit.Wpf.AutoCompleteComboBox.
/// </summary>
public class AutoCompleteComboBoxSetting
{
    /// <summary>
    /// Gets a filter predicate for the given query text.
    /// Default: case-insensitive contains match.
    /// </summary>
    public virtual Predicate<object> GetFilter(string query, Func<object, string> stringFromItem)
    {
        return item => stringFromItem(item).Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Maximum number of suggestions before the dropdown auto-opens.
    /// Higher values increase filtering cost.
    /// </summary>
    public virtual int MaxSuggestionCount => 100;

    /// <summary>
    /// Delay before updating the suggestion list after typing. Zero = immediate.
    /// </summary>
    public virtual TimeSpan Delay => TimeSpan.FromMilliseconds(150);

    private static AutoCompleteComboBoxSetting _default = new();

    public static AutoCompleteComboBoxSetting Default
    {
        get => _default;
        set => _default = value ?? throw new ArgumentNullException(nameof(value));
    }
}

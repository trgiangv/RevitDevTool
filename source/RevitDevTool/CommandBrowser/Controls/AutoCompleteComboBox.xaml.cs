using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Binding = System.Windows.Data.Binding;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

namespace RevitDevTool.CommandBrowser.Controls;

/// <summary>
/// ComboBox with autocomplete/filtering support.
/// Ported from DotNetKit.Wpf.AutoCompleteComboBox, adapted for RevitDevTool namespaces.
/// </summary>
public partial class AutoCompleteComboBox
{
    private DispatcherTimer? _debounceTimer;
    private Predicate<object>? _defaultItemsFilter;
    private string? _previousText;
    private BindingEvaluator<string>? _itemTextEvaluator;
    private string? _cachedFilterQuery;
    private Predicate<object>? _cachedFilter;

    [UsedImplicitly]
    public System.Windows.Controls.TextBox? EditableTextBox
    {
        get
        {
            field ??= FindDescendant(this, "PART_EditableTextBox") as System.Windows.Controls.TextBox;
            return field;
        }
    }

    private string GetItemText(object? item)
    {
        if (item is null) return string.Empty;

        _itemTextEvaluator ??= new BindingEvaluator<string>();
        _itemTextEvaluator.SetBinding(item, TextSearch.GetTextPath(this));
        return _itemTextEvaluator.Value ?? string.Empty;
    }

    protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
    {
        base.OnItemsSourceChanged(oldValue, newValue);
        _defaultItemsFilter = newValue is ICollectionView cv ? cv.Filter : null;
    }

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

    private readonly struct TextBoxStateSaver(System.Windows.Controls.TextBox? textBox) : IDisposable
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
        using (Items.DeferRefresh())
        {
            Items.Filter = filter;
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
            if (controlOpen) IsDropDownOpen = false;
            SelectedItem = null;
            using (Items.DeferRefresh())
            {
                Items.Filter = _defaultItemsFilter;
            }
            _cachedFilterQuery = null;
            _cachedFilter = null;
        }
        else if (SelectedItem is not null && GetItemText(SelectedItem) == text)
        {
            // ignore
        }
        else
        {
            using (new TextBoxStateSaver(EditableTextBox))
            {
                SelectedItem = null;
            }

            UpdateFilter();

            if (controlOpen && !IsDropDownOpen && IsKeyboardFocusWithin && Items.Count > 0)
            {
                IsDropDownOpen = true;
            }
        }
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

        _debounceTimer?.Stop();

        UpdateSuggestionList(controlOpen: false);

        var textBox = EditableTextBox;
        if (textBox is not null)
            textBox.Select(textBox.SelectionStart + textBox.SelectionLength, 0);
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

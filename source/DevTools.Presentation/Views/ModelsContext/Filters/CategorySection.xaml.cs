using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Scope;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace DevTools.Presentation.Views.ModelsContext.Filters;

public partial class CategorySection : UserControl
{
    public static readonly DependencyProperty PickerProperty = DependencyProperty.Register(
        nameof(Picker), typeof(object), typeof(CategorySection), new PropertyMetadata(null, OnPickerChanged));

    public static readonly DependencyProperty AutomationPrefixProperty = DependencyProperty.Register(
        nameof(AutomationPrefix), typeof(string), typeof(CategorySection), new PropertyMetadata("Filter"));

    public static readonly DependencyProperty ShowEyeProperty = DependencyProperty.Register(
        nameof(ShowEye), typeof(bool), typeof(CategorySection), new PropertyMetadata(false));

    public static readonly DependencyProperty IsLockedProperty = DependencyProperty.Register(
        nameof(IsLocked), typeof(bool), typeof(CategorySection), new PropertyMetadata(false));

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(CategorySection),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty ItemMenuProperty = DependencyProperty.Register(
        nameof(ItemMenu), typeof(ContextMenu), typeof(CategorySection));

    public static readonly DependencyProperty MenuContextProperty = DependencyProperty.Register(
        nameof(MenuContext), typeof(object), typeof(CategorySection));

    public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(
        nameof(SelectionMode), typeof(SelectionMode), typeof(CategorySection), new PropertyMetadata(SelectionMode.Single));

    private bool _syncing;

    public CategorySection()
    {
        InitializeComponent();
        ((INotifyCollectionChanged)CategoryList.Items).CollectionChanged += (_, _) => SyncSelection();
    }

    public SelectionMode SelectionMode
    {
        get => (SelectionMode)GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    private static void OnPickerChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        var section = (CategorySection)owner;
        if (e.OldValue is CategoryPickerViewModel old)
        {
            old.TicksChanged -= section.OnTicksChanged;
        }

        if (e.NewValue is CategoryPickerViewModel picker)
        {
            picker.TicksChanged += section.OnTicksChanged;
        }

        section.SyncSelection();
    }

    private void OnTicksChanged(object? sender, EventArgs e)
    {
        SyncSelection();
    }

    private void SyncSelection()
    {
        if (SelectionMode != SelectionMode.Extended || _syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            var items = CategoryList.Items.OfType<CategoryItemViewModel>().ToList();
            var selected = CategoryList.SelectedItems;
            foreach (var item in items.Where(item => !item.IsChecked && selected.Contains(item)))
            {
                selected.Remove(item);
            }

            foreach (var item in items.Where(item => item.IsChecked && !selected.Contains(item)))
            {
                selected.Add(item);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectionMode != SelectionMode.Extended || _syncing || Picker is not CategoryPickerViewModel picker)
        {
            return;
        }

        _syncing = true;
        try
        {
            var listed = CategoryList.Items;
            picker.SetTicks(e.AddedItems.OfType<CategoryItemViewModel>(),
                e.RemovedItems.OfType<CategoryItemViewModel>().Where(listed.Contains));
        }
        finally
        {
            _syncing = false;
        }
    }

    public object? Picker
    {
        get => GetValue(PickerProperty);
        set => SetValue(PickerProperty, value);
    }

    public string AutomationPrefix
    {
        get => (string)GetValue(AutomationPrefixProperty);
        set => SetValue(AutomationPrefixProperty, value);
    }

    public bool ShowEye
    {
        get => (bool)GetValue(ShowEyeProperty);
        set => SetValue(ShowEyeProperty, value);
    }

    public bool IsLocked
    {
        get => (bool)GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public ContextMenu? ItemMenu
    {
        get => (ContextMenu?)GetValue(ItemMenuProperty);
        set => SetValue(ItemMenuProperty, value);
    }

    public object? MenuContext
    {
        get => GetValue(MenuContextProperty);
        set => SetValue(MenuContextProperty, value);
    }
}

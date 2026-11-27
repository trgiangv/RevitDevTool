using DevTools.ModelsContext.Filters;
using DevTools.Presentation.Views.ModelsContext.Panel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DevTools.Presentation.Views.ModelsContext.Filters;

public partial class CategorySection : UserControl
{
    public static readonly DependencyProperty PickerProperty = DependencyProperty.Register(
        nameof(Picker), typeof(object), typeof(CategorySection), new PropertyMetadata(null));

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

    public CategorySection()
    {
        InitializeComponent();
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers != ModifierKeys.None || IsLocked
            || e.OriginalSource is not ListBoxItem { DataContext: CategoryItemViewModel item })
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Space:
                e.Handled = true;
                item.IsChecked = !item.IsChecked;
                break;
            case Key.Right when item.HasChildren:
                e.Handled = true;
                item.IsExpanded = true;
                break;
            case Key.Left when item.HasChildren:
                e.Handled = true;
                item.IsExpanded = false;
                break;
        }
    }

    private void OnRowGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, sender))
        {
            e.Handled = true;
        }
    }

    private void OnRowPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        RowButtons.SwallowDisabledClick(sender, e);
    }

    private void OnListRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
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

using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
// ReSharper disable once CheckNamespace
namespace DevTools.UI.Controls;

/// <summary>
/// Drop-down color picker with palette and advanced tabs. Adapted from MahApps.Metro, styled with HandyControl.
/// </summary>
[TemplatePart(Name = PartPopup, Type = typeof(Popup))]
[TemplatePart(Name = PartColorPaletteStandard, Type = typeof(ColorPalette))]
[TemplatePart(Name = PartColorPaletteAvailable, Type = typeof(ColorPalette))]
[TemplatePart(Name = PartColorPaletteCustom01, Type = typeof(ColorPalette))]
[TemplatePart(Name = PartColorPaletteCustom02, Type = typeof(ColorPalette))]
[TemplatePart(Name = PartColorPaletteRecent, Type = typeof(ColorPalette))]
[TemplatePart(Name = PartPopupTabControl, Type = typeof(TabControl))]
[TemplatePart(Name = PartColorPalettesTab, Type = typeof(TabItem))]
[TemplatePart(Name = PartAdvancedTab, Type = typeof(TabItem))]
[StyleTypedProperty(Property = nameof(StandardColorPaletteStyle), StyleTargetType = typeof(ColorPalette))]
[StyleTypedProperty(Property = nameof(AvailableColorPaletteStyle), StyleTargetType = typeof(ColorPalette))]
[StyleTypedProperty(Property = nameof(CustomColorPalette01Style), StyleTargetType = typeof(ColorPalette))]
[StyleTypedProperty(Property = nameof(CustomColorPalette02Style), StyleTargetType = typeof(ColorPalette))]
[StyleTypedProperty(Property = nameof(RecentColorPaletteStyle), StyleTargetType = typeof(ColorPalette))]
[StyleTypedProperty(Property = nameof(TabControlStyle), StyleTargetType = typeof(TabControl))]
[StyleTypedProperty(Property = nameof(TabItemStyle), StyleTargetType = typeof(TabItem))]
public class ColorPicker : ColorPickerBase
{
    internal const string PartPopup = "PART_Popup";
    internal const string PartColorPaletteStandard = "PART_ColorPaletteStandard";
    internal const string PartColorPaletteAvailable = "PART_ColorPaletteAvailable";
    internal const string PartColorPaletteCustom01 = "PART_ColorPaletteCustom01";
    internal const string PartColorPaletteCustom02 = "PART_ColorPaletteCustom02";
    internal const string PartColorPaletteRecent = "PART_ColorPaletteRecent";
    internal const string PartPopupTabControl = "PART_PopupTabControl";
    internal const string PartColorPalettesTab = "PART_ColorPalettesTab";
    internal const string PartAdvancedTab = "PART_AdvancedTab";

    private ColorPalette? _standardPalette;
    private ColorPalette? _availablePalette;
    private ColorPalette? _customPalette01;
    private ColorPalette? _customPalette02;
    private ColorPalette? _recentPalette;
    private TabControl? _tabControl;
    private TabItem? _palettesTab;
    private TabItem? _advancedTab;

    public static readonly RoutedEvent DropDownClosedEvent =
        EventManager.RegisterRoutedEvent(nameof(DropDownClosed), RoutingStrategy.Bubble, typeof(EventHandler<EventArgs>), typeof(ColorPicker));

    public static readonly RoutedEvent DropDownOpenedEvent =
        EventManager.RegisterRoutedEvent(nameof(DropDownOpened), RoutingStrategy.Bubble, typeof(EventHandler<EventArgs>), typeof(ColorPicker));

    public static readonly DependencyProperty DropDownHeightProperty =
        DependencyProperty.Register(nameof(DropDownHeight), typeof(double), typeof(ColorPicker), new PropertyMetadata(360d));

    public static readonly DependencyProperty DropDownWidthProperty =
        DependencyProperty.Register(nameof(DropDownWidth), typeof(double), typeof(ColorPicker), new PropertyMetadata(500d));

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(ColorPicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsDropDownOpenChanged));

    public static readonly DependencyProperty SelectedColorTemplateProperty =
        DependencyProperty.Register(nameof(SelectedColorTemplate), typeof(DataTemplate), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty AddToRecentColorsTriggerProperty =
        DependencyProperty.Register(nameof(AddToRecentColorsTrigger), typeof(AddToRecentColorsTrigger), typeof(ColorPicker), new PropertyMetadata(AddToRecentColorsTrigger.ColorPickerClosed));

    public static readonly DependencyProperty IsAvailableColorPaletteVisibleProperty =
        DependencyProperty.Register(nameof(IsAvailableColorPaletteVisible), typeof(bool), typeof(ColorPicker), new PropertyMetadata(true));

    public static readonly DependencyProperty AvailableColorPaletteHeaderProperty =
        DependencyProperty.Register(nameof(AvailableColorPaletteHeader), typeof(object), typeof(ColorPicker), new PropertyMetadata("Available"));

    public static readonly DependencyProperty AvailableColorPaletteHeaderTemplateProperty =
        DependencyProperty.Register(nameof(AvailableColorPaletteHeaderTemplate), typeof(DataTemplate), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty AvailableColorPaletteItemsSourceProperty =
        DependencyProperty.Register(nameof(AvailableColorPaletteItemsSource), typeof(IEnumerable), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty AvailableColorPaletteStyleProperty =
        DependencyProperty.Register(nameof(AvailableColorPaletteStyle), typeof(Style), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty IsCustomColorPalette01VisibleProperty =
        DependencyProperty.Register(nameof(IsCustomColorPalette01Visible), typeof(bool), typeof(ColorPicker), new PropertyMetadata(false));

    public static readonly DependencyProperty CustomColorPalette01HeaderProperty =
        DependencyProperty.Register(nameof(CustomColorPalette01Header), typeof(object), typeof(ColorPicker), new PropertyMetadata("Custom 01"));

    public static readonly DependencyProperty CustomColorPalette01HeaderTemplateProperty =
        DependencyProperty.Register(nameof(CustomColorPalette01HeaderTemplate), typeof(DataTemplate), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty CustomColorPalette01ItemsSourceProperty =
        DependencyProperty.Register(nameof(CustomColorPalette01ItemsSource), typeof(IEnumerable), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty CustomColorPalette01StyleProperty =
        DependencyProperty.Register(nameof(CustomColorPalette01Style), typeof(Style), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty IsCustomColorPalette02VisibleProperty =
        DependencyProperty.Register(nameof(IsCustomColorPalette02Visible), typeof(bool), typeof(ColorPicker), new PropertyMetadata(false));

    public static readonly DependencyProperty CustomColorPalette02HeaderProperty =
        DependencyProperty.Register(nameof(CustomColorPalette02Header), typeof(object), typeof(ColorPicker), new PropertyMetadata("Custom 02"));

    public static readonly DependencyProperty CustomColorPalette02HeaderTemplateProperty =
        DependencyProperty.Register(nameof(CustomColorPalette02HeaderTemplate), typeof(DataTemplate), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty CustomColorPalette02ItemsSourceProperty =
        DependencyProperty.Register(nameof(CustomColorPalette02ItemsSource), typeof(IEnumerable), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty CustomColorPalette02StyleProperty =
        DependencyProperty.Register(nameof(CustomColorPalette02Style), typeof(Style), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty IsRecentColorPaletteVisibleProperty =
        DependencyProperty.Register(nameof(IsRecentColorPaletteVisible), typeof(bool), typeof(ColorPicker), new PropertyMetadata(true));

    public static readonly DependencyProperty RecentColorPaletteHeaderProperty =
        DependencyProperty.Register(nameof(RecentColorPaletteHeader), typeof(object), typeof(ColorPicker), new PropertyMetadata("Recent"));

    public static readonly DependencyProperty RecentColorPaletteHeaderTemplateProperty =
        DependencyProperty.Register(nameof(RecentColorPaletteHeaderTemplate), typeof(DataTemplate), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty RecentColorPaletteItemsSourceProperty =
        DependencyProperty.Register(nameof(RecentColorPaletteItemsSource), typeof(IEnumerable), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty RecentColorPaletteStyleProperty =
        DependencyProperty.Register(nameof(RecentColorPaletteStyle), typeof(Style), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty IsStandardColorPaletteVisibleProperty =
        DependencyProperty.Register(nameof(IsStandardColorPaletteVisible), typeof(bool), typeof(ColorPicker), new PropertyMetadata(true));

    public static readonly DependencyProperty StandardColorPaletteHeaderProperty =
        DependencyProperty.Register(nameof(StandardColorPaletteHeader), typeof(object), typeof(ColorPicker), new PropertyMetadata("Standard"));

    public static readonly DependencyProperty StandardColorPaletteHeaderTemplateProperty =
        DependencyProperty.Register(nameof(StandardColorPaletteHeaderTemplate), typeof(DataTemplate), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty StandardColorPaletteItemsSourceProperty =
        DependencyProperty.Register(nameof(StandardColorPaletteItemsSource), typeof(IEnumerable), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty StandardColorPaletteStyleProperty =
        DependencyProperty.Register(nameof(StandardColorPaletteStyle), typeof(Style), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty TabControlStyleProperty =
        DependencyProperty.Register(nameof(TabControlStyle), typeof(Style), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty TabItemStyleProperty =
        DependencyProperty.Register(nameof(TabItemStyle), typeof(Style), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty ColorPalettesTabHeaderProperty =
        DependencyProperty.Register(nameof(ColorPalettesTabHeader), typeof(object), typeof(ColorPicker), new PropertyMetadata("Palettes"));

    public static readonly DependencyProperty ColorPalettesTabHeaderTemplateProperty =
        DependencyProperty.Register(nameof(ColorPalettesTabHeaderTemplate), typeof(DataTemplate), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty IsColorPalettesTabVisibleProperty =
        DependencyProperty.Register(nameof(IsColorPalettesTabVisible), typeof(bool), typeof(ColorPicker), new PropertyMetadata(true, OnIsTabVisiblePropertyChanged));

    public static readonly DependencyProperty AdvancedTabHeaderProperty =
        DependencyProperty.Register(nameof(AdvancedTabHeader), typeof(object), typeof(ColorPicker), new PropertyMetadata("Advanced"));

    public static readonly DependencyProperty AdvancedTabHeaderTemplateProperty =
        DependencyProperty.Register(nameof(AdvancedTabHeaderTemplate), typeof(DataTemplate), typeof(ColorPicker), new PropertyMetadata(null));

    public static readonly DependencyProperty IsAdvancedTabVisibleProperty =
        DependencyProperty.Register(nameof(IsAdvancedTabVisible), typeof(bool), typeof(ColorPicker), new PropertyMetadata(true, OnIsTabVisiblePropertyChanged));

    public static readonly DependencyProperty CloseOnSelectedColorChangedProperty =
        DependencyProperty.Register(nameof(CloseOnSelectedColorChanged), typeof(bool), typeof(ColorPicker), new PropertyMetadata(false));

    static ColorPicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorPicker), new FrameworkPropertyMetadata(typeof(ColorPicker)));
        EventManager.RegisterClassHandler(typeof(ColorPicker), Mouse.LostMouseCaptureEvent, new MouseEventHandler(OnLostMouseCapture));
        EventManager.RegisterClassHandler(typeof(ColorPicker), Mouse.MouseDownEvent, new MouseButtonEventHandler(OnMouseDown), true);
    }

    public event EventHandler<EventArgs> DropDownClosed
    {
        add => AddHandler(DropDownClosedEvent, value);
        remove => RemoveHandler(DropDownClosedEvent, value);
    }

    public event EventHandler<EventArgs> DropDownOpened
    {
        add => AddHandler(DropDownOpenedEvent, value);
        remove => RemoveHandler(DropDownOpenedEvent, value);
    }

    public double DropDownHeight
    {
        get => (double)GetValue(DropDownHeightProperty);
        set => SetValue(DropDownHeightProperty, value);
    }

    [Bindable(true)]
    [Category("Layout")]
    [TypeConverter(typeof(LengthConverter))]
    public double DropDownWidth
    {
        get => (double)GetValue(DropDownWidthProperty);
        set => SetValue(DropDownWidthProperty, value);
    }

    [Bindable(true)]
    [Browsable(false)]
    [Category("Appearance")]
    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public DataTemplate? SelectedColorTemplate
    {
        get => (DataTemplate?)GetValue(SelectedColorTemplateProperty);
        set => SetValue(SelectedColorTemplateProperty, value);
    }

    public AddToRecentColorsTrigger AddToRecentColorsTrigger
    {
        get => (AddToRecentColorsTrigger)GetValue(AddToRecentColorsTriggerProperty);
        set => SetValue(AddToRecentColorsTriggerProperty, value);
    }

    public bool IsAvailableColorPaletteVisible
    {
        get => (bool)GetValue(IsAvailableColorPaletteVisibleProperty);
        set => SetValue(IsAvailableColorPaletteVisibleProperty, value);
    }

    public object AvailableColorPaletteHeader
    {
        get => GetValue(AvailableColorPaletteHeaderProperty);
        set => SetValue(AvailableColorPaletteHeaderProperty, value);
    }

    public DataTemplate? AvailableColorPaletteHeaderTemplate
    {
        get => (DataTemplate?)GetValue(AvailableColorPaletteHeaderTemplateProperty);
        set => SetValue(AvailableColorPaletteHeaderTemplateProperty, value);
    }

    public IEnumerable? AvailableColorPaletteItemsSource
    {
        get => (IEnumerable?)GetValue(AvailableColorPaletteItemsSourceProperty);
        set => SetValue(AvailableColorPaletteItemsSourceProperty, value);
    }

    public Style? AvailableColorPaletteStyle
    {
        get => (Style?)GetValue(AvailableColorPaletteStyleProperty);
        set => SetValue(AvailableColorPaletteStyleProperty, value);
    }

    public bool IsCustomColorPalette01Visible
    {
        get => (bool)GetValue(IsCustomColorPalette01VisibleProperty);
        set => SetValue(IsCustomColorPalette01VisibleProperty, value);
    }

    public object CustomColorPalette01Header
    {
        get => GetValue(CustomColorPalette01HeaderProperty);
        set => SetValue(CustomColorPalette01HeaderProperty, value);
    }

    public DataTemplate? CustomColorPalette01HeaderTemplate
    {
        get => (DataTemplate?)GetValue(CustomColorPalette01HeaderTemplateProperty);
        set => SetValue(CustomColorPalette01HeaderTemplateProperty, value);
    }

    public IEnumerable? CustomColorPalette01ItemsSource
    {
        get => (IEnumerable?)GetValue(CustomColorPalette01ItemsSourceProperty);
        set => SetValue(CustomColorPalette01ItemsSourceProperty, value);
    }

    public Style? CustomColorPalette01Style
    {
        get => (Style?)GetValue(CustomColorPalette01StyleProperty);
        set => SetValue(CustomColorPalette01StyleProperty, value);
    }

    public bool IsCustomColorPalette02Visible
    {
        get => (bool)GetValue(IsCustomColorPalette02VisibleProperty);
        set => SetValue(IsCustomColorPalette02VisibleProperty, value);
    }

    public object CustomColorPalette02Header
    {
        get => GetValue(CustomColorPalette02HeaderProperty);
        set => SetValue(CustomColorPalette02HeaderProperty, value);
    }

    public DataTemplate? CustomColorPalette02HeaderTemplate
    {
        get => (DataTemplate?)GetValue(CustomColorPalette02HeaderTemplateProperty);
        set => SetValue(CustomColorPalette02HeaderTemplateProperty, value);
    }

    public IEnumerable? CustomColorPalette02ItemsSource
    {
        get => (IEnumerable?)GetValue(CustomColorPalette02ItemsSourceProperty);
        set => SetValue(CustomColorPalette02ItemsSourceProperty, value);
    }

    public Style? CustomColorPalette02Style
    {
        get => (Style?)GetValue(CustomColorPalette02StyleProperty);
        set => SetValue(CustomColorPalette02StyleProperty, value);
    }

    public bool IsRecentColorPaletteVisible
    {
        get => (bool)GetValue(IsRecentColorPaletteVisibleProperty);
        set => SetValue(IsRecentColorPaletteVisibleProperty, value);
    }

    public object RecentColorPaletteHeader
    {
        get => GetValue(RecentColorPaletteHeaderProperty);
        set => SetValue(RecentColorPaletteHeaderProperty, value);
    }

    public DataTemplate? RecentColorPaletteHeaderTemplate
    {
        get => (DataTemplate?)GetValue(RecentColorPaletteHeaderTemplateProperty);
        set => SetValue(RecentColorPaletteHeaderTemplateProperty, value);
    }

    public IEnumerable? RecentColorPaletteItemsSource
    {
        get => (IEnumerable?)GetValue(RecentColorPaletteItemsSourceProperty);
        set => SetValue(RecentColorPaletteItemsSourceProperty, value);
    }

    public Style? RecentColorPaletteStyle
    {
        get => (Style?)GetValue(RecentColorPaletteStyleProperty);
        set => SetValue(RecentColorPaletteStyleProperty, value);
    }

    public bool IsStandardColorPaletteVisible
    {
        get => (bool)GetValue(IsStandardColorPaletteVisibleProperty);
        set => SetValue(IsStandardColorPaletteVisibleProperty, value);
    }

    public object StandardColorPaletteHeader
    {
        get => GetValue(StandardColorPaletteHeaderProperty);
        set => SetValue(StandardColorPaletteHeaderProperty, value);
    }

    public DataTemplate? StandardColorPaletteHeaderTemplate
    {
        get => (DataTemplate?)GetValue(StandardColorPaletteHeaderTemplateProperty);
        set => SetValue(StandardColorPaletteHeaderTemplateProperty, value);
    }

    public IEnumerable? StandardColorPaletteItemsSource
    {
        get => (IEnumerable?)GetValue(StandardColorPaletteItemsSourceProperty);
        set => SetValue(StandardColorPaletteItemsSourceProperty, value);
    }

    public Style? StandardColorPaletteStyle
    {
        get => (Style?)GetValue(StandardColorPaletteStyleProperty);
        set => SetValue(StandardColorPaletteStyleProperty, value);
    }

    public Style? TabControlStyle
    {
        get => (Style?)GetValue(TabControlStyleProperty);
        set => SetValue(TabControlStyleProperty, value);
    }

    public Style? TabItemStyle
    {
        get => (Style?)GetValue(TabItemStyleProperty);
        set => SetValue(TabItemStyleProperty, value);
    }

    public object ColorPalettesTabHeader
    {
        get => GetValue(ColorPalettesTabHeaderProperty);
        set => SetValue(ColorPalettesTabHeaderProperty, value);
    }

    public DataTemplate? ColorPalettesTabHeaderTemplate
    {
        get => (DataTemplate?)GetValue(ColorPalettesTabHeaderTemplateProperty);
        set => SetValue(ColorPalettesTabHeaderTemplateProperty, value);
    }

    public bool IsColorPalettesTabVisible
    {
        get => (bool)GetValue(IsColorPalettesTabVisibleProperty);
        set => SetValue(IsColorPalettesTabVisibleProperty, value);
    }

    public object AdvancedTabHeader
    {
        get => GetValue(AdvancedTabHeaderProperty);
        set => SetValue(AdvancedTabHeaderProperty, value);
    }

    public DataTemplate? AdvancedTabHeaderTemplate
    {
        get => (DataTemplate?)GetValue(AdvancedTabHeaderTemplateProperty);
        set => SetValue(AdvancedTabHeaderTemplateProperty, value);
    }

    public bool IsAdvancedTabVisible
    {
        get => (bool)GetValue(IsAdvancedTabVisibleProperty);
        set => SetValue(IsAdvancedTabVisibleProperty, value);
    }

    public bool CloseOnSelectedColorChanged
    {
        get => (bool)GetValue(CloseOnSelectedColorChangedProperty);
        set => SetValue(CloseOnSelectedColorChangedProperty, value);
    }

    public override void OnApplyTemplate()
    {
        UnhookPalettes();
        base.OnApplyTemplate();

        _standardPalette = GetTemplateChild(PartColorPaletteStandard) as ColorPalette;
        _availablePalette = GetTemplateChild(PartColorPaletteAvailable) as ColorPalette;
        _customPalette01 = GetTemplateChild(PartColorPaletteCustom01) as ColorPalette;
        _customPalette02 = GetTemplateChild(PartColorPaletteCustom02) as ColorPalette;
        _recentPalette = GetTemplateChild(PartColorPaletteRecent) as ColorPalette;
        _tabControl = GetTemplateChild(PartPopupTabControl) as TabControl;
        _palettesTab = GetTemplateChild(PartColorPalettesTab) as TabItem;
        _advancedTab = GetTemplateChild(PartAdvancedTab) as TabItem;

        HookPalettes();
        ValidateTabItems();
    }

    internal override void OnSelectedColorChanged(Color? oldValue, Color? newValue)
    {
        base.OnSelectedColorChanged(oldValue, newValue);

        _availablePalette?.SetCurrentValue(Selector.SelectedValueProperty, newValue);
        _standardPalette?.SetCurrentValue(Selector.SelectedValueProperty, newValue);
        _customPalette01?.SetCurrentValue(Selector.SelectedValueProperty, newValue);
        _customPalette02?.SetCurrentValue(Selector.SelectedValueProperty, newValue);
        _recentPalette?.SetCurrentValue(Selector.SelectedValueProperty, newValue);

        if (AddToRecentColorsTrigger == AddToRecentColorsTrigger.SelectedColorChanged && SelectedColor.HasValue)
        {
            BuiltInColorPalettes.AddColorToRecentColors(newValue, RecentColorPaletteItemsSource, BuiltInColorPalettes.GetMaximumRecentColorsCount(this));
        }
    }

    private void HookPalettes()
    {
        if (_standardPalette is not null)
        {
            _standardPalette.SelectionChanged += ColorPalette_SelectionChanged;
        }

        if (_availablePalette is not null)
        {
            _availablePalette.SelectionChanged += ColorPalette_SelectionChanged;
        }

        if (_customPalette01 is not null)
        {
            _customPalette01.SelectionChanged += ColorPalette_SelectionChanged;
        }

        if (_customPalette02 is not null)
        {
            _customPalette02.SelectionChanged += ColorPalette_SelectionChanged;
        }

        if (_recentPalette is not null)
        {
            _recentPalette.SelectionChanged += ColorPalette_SelectionChanged;
        }
    }

    private void UnhookPalettes()
    {
        if (_standardPalette is not null)
        {
            _standardPalette.SelectionChanged -= ColorPalette_SelectionChanged;
        }

        if (_availablePalette is not null)
        {
            _availablePalette.SelectionChanged -= ColorPalette_SelectionChanged;
        }

        if (_customPalette01 is not null)
        {
            _customPalette01.SelectionChanged -= ColorPalette_SelectionChanged;
        }

        if (_customPalette02 is not null)
        {
            _customPalette02.SelectionChanged -= ColorPalette_SelectionChanged;
        }

        if (_recentPalette is not null)
        {
            _recentPalette.SelectionChanged -= ColorPalette_SelectionChanged;
        }
    }

    private void ColorPalette_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ColorPalette colorPalette && !ColorIsUpdating)
        {
            SetCurrentValue(SelectedColorProperty, colorPalette.SelectedItem as Color?);
            if (CloseOnSelectedColorChanged)
            {
                Close();
            }
        }
    }

    private static void OnIsDropDownOpenChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ColorPicker colorPicker)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            colorPicker.RaiseEvent(new RoutedEventArgs(DropDownOpenedEvent));
            colorPicker.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(colorPicker.FocusDropDown));
            return;
        }

        colorPicker.RaiseEvent(new RoutedEventArgs(DropDownClosedEvent));
        if (ReferenceEquals(Mouse.Captured, colorPicker))
        {
            Mouse.Capture(null);
        }

        if (colorPicker.AddToRecentColorsTrigger == AddToRecentColorsTrigger.ColorPickerClosed && colorPicker.SelectedColor.HasValue)
        {
            BuiltInColorPalettes.AddColorToRecentColors(colorPicker.SelectedColor, colorPicker.RecentColorPaletteItemsSource, BuiltInColorPalettes.GetMaximumRecentColorsCount(colorPicker));
        }
    }

    private void FocusDropDown()
    {
        Focus();
        Mouse.Capture(this, CaptureMode.SubTree);
        ValidateTabItems();

        if (_tabControl is null)
        {
            return;
        }

        if (ReferenceEquals(_tabControl.SelectedItem, _palettesTab))
        {
            if (IsStandardColorPaletteVisible && _standardPalette is not null)
            {
                _standardPalette.FocusSelectedItem();
            }
            else if (IsAvailableColorPaletteVisible && _availablePalette is not null)
            {
                _availablePalette.FocusSelectedItem();
            }
            else if (IsCustomColorPalette01Visible && _customPalette01 is not null)
            {
                _customPalette01.FocusSelectedItem();
            }
            else if (IsCustomColorPalette02Visible && _customPalette02 is not null)
            {
                _customPalette02.FocusSelectedItem();
            }
            else if (IsRecentColorPaletteVisible && _recentPalette is not null)
            {
                _recentPalette.FocusSelectedItem();
            }
        }
        else if (ReferenceEquals(_tabControl.SelectedItem, _advancedTab))
        {
            _advancedTab?.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }
    }

    private static void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        var colorPicker = (ColorPicker)sender;
        if (ReferenceEquals(Mouse.Captured, colorPicker))
        {
            return;
        }

        if (ReferenceEquals(e.OriginalSource, colorPicker))
        {
            // A drag on the advanced surface, a slider, or the eye dropper takes capture.
            // Those elements live in the popup, so the dropdown has to stay open.
            if (Mouse.Captured is not DependencyObject captured || !IsDescendantOf(captured, colorPicker))
            {
                colorPicker.Close();
            }

            return;
        }

        if (e.OriginalSource is DependencyObject source && IsDescendantOf(source, colorPicker))
        {
            if (colorPicker.IsDropDownOpen && Mouse.Captured is null)
            {
                Mouse.Capture(colorPicker, CaptureMode.SubTree);
                e.Handled = true;
            }

            return;
        }

        colorPicker.Close();
    }

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var colorPicker = (ColorPicker)sender;
        if ((colorPicker.ContextMenu is null || !colorPicker.ContextMenu.IsOpen) && !colorPicker.IsKeyboardFocusWithin)
        {
            colorPicker.Focus();
        }

        // MouseLeftButtonDown is promoted only when MouseDown stays unhandled. Marking this handled
        // stops that promotion, so the header toggle never receives the click. Open and close here.
        if (e.ChangedButton == MouseButton.Left && IsHeaderSource(colorPicker, e.OriginalSource))
        {
            if (colorPicker.IsDropDownOpen)
            {
                colorPicker.Close();
            }
            else
            {
                colorPicker.SetCurrentValue(IsDropDownOpenProperty, true);
            }

            e.Handled = true;
            return;
        }

        // Leave popup content unhandled so MouseLeftButtonDown still promotes and a drag can start.
        if (!IsPopupSource(colorPicker, e.OriginalSource))
        {
            e.Handled = true;
        }
    }

    private static bool IsHeaderSource(ColorPicker picker, object? source)
    {
        if (source is not DependencyObject node || IsPopupSource(picker, source))
        {
            return false;
        }

        return ReferenceEquals(node, picker) || IsInside(node, picker);
    }

    private static bool IsPopupSource(ColorPicker picker, object? source)
    {
        return source is DependencyObject node
               && picker.GetTemplateChild(PartPopup) is Popup { Child: DependencyObject popupChild }
               && IsInside(node, popupChild);
    }

    private static bool IsInside(DependencyObject node, DependencyObject ancestor)
    {
        var current = node;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            if (current is Popup)
            {
                return false;
            }

            current = current is Visual
                ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private static bool IsDescendantOf(DependencyObject node, DependencyObject reference)
    {
        var current = node;
        while (current is not null)
        {
            if (ReferenceEquals(current, reference))
            {
                return true;
            }

            current = GetParentObject(current);
        }

        return false;
    }

    /// <summary>
    /// Walks visual parents, then logical parents. Popup content is parented to a separate
    /// window visually; its logical parent is the <see cref="Popup"/>, which links back to the picker.
    /// Same rule as MahApps <c>TreeHelper</c>.
    /// </summary>
    private static DependencyObject? GetParentObject(DependencyObject child)
    {
        if (child is Popup popup)
        {
            return popup.Parent ?? popup.PlacementTarget;
        }

        if (LogicalTreeHelper.GetParent(child) is Popup logicalPopup)
        {
            return logicalPopup;
        }

        if (child is Visual or Visual3D)
        {
            var visualParent = VisualTreeHelper.GetParent(child);
            if (visualParent is not null)
            {
                return visualParent;
            }
        }

        if (child is FrameworkElement { Parent: { } parent })
        {
            return parent;
        }

        if (child is ContentElement contentElement)
        {
            var contentParent = ContentOperations.GetParent(contentElement);
            if (contentParent is not null)
            {
                return contentParent;
            }

            if (contentElement is FrameworkContentElement frameworkContentElement)
            {
                return frameworkContentElement.Parent;
            }
        }

        return LogicalTreeHelper.GetParent(child);
    }

    private void Close()
    {
        if (IsDropDownOpen)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
        }
    }

    private static void OnIsTabVisiblePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is ColorPicker colorPicker && colorPicker.IsInitialized)
        {
            colorPicker.ValidateTabItems();
        }
    }

    private void ValidateTabItems()
    {
        if (_tabControl is null || _palettesTab is null || _advancedTab is null)
        {
            return;
        }

        if (!IsAdvancedTabVisible && !IsColorPalettesTabVisible)
        {
            _tabControl.SelectedIndex = -1;
        }
        else if (ReferenceEquals(_tabControl.SelectedItem, _palettesTab) && !IsColorPalettesTabVisible)
        {
            _tabControl.SelectedItem = _advancedTab;
        }
        else if (ReferenceEquals(_tabControl.SelectedItem, _advancedTab) && !IsAdvancedTabVisible)
        {
            _tabControl.SelectedItem = _palettesTab;
        }
        else if (IsColorPalettesTabVisible)
        {
            _palettesTab.IsSelected = true;
        }
        else if (IsAdvancedTabVisible)
        {
            _advancedTab.IsSelected = true;
        }
    }
}

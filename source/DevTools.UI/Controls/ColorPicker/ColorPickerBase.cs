using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
// ReSharper disable once CheckNamespace
namespace DevTools.UI.Controls;

/// <summary>
/// Shared color state for <see cref="ColorPicker"/> and <see cref="ColorCanvas"/>. Adapted from MahApps.Metro.
/// </summary>
[TemplatePart(Name = PartSaturationValueBox, Type = typeof(FrameworkElement))]
public class ColorPickerBase : Control
{
    internal const string PartSaturationValueBox = "PART_SaturationValueBox";

    protected bool ColorIsUpdating;

    protected bool UpdateHsvValues = true;

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color?),
            typeof(ColorPickerBase),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorPropertyChanged, CoerceSelectedColor));

    public static readonly DependencyProperty DefaultColorProperty =
        DependencyProperty.Register(
            nameof(DefaultColor),
            typeof(Color?),
            typeof(ColorPickerBase),
            new FrameworkPropertyMetadata(null, OnDefaultColorPropertyChanged));

    private static readonly DependencyPropertyKey SelectedHsvColorPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(SelectedHsvColor),
            typeof(HsvColor),
            typeof(ColorPickerBase),
            new PropertyMetadata(new HsvColor(Colors.Black)));

    public static readonly DependencyProperty SelectedHsvColorProperty = SelectedHsvColorPropertyKey.DependencyProperty;

    public static readonly DependencyProperty ColorNameProperty =
        DependencyProperty.Register(
            nameof(ColorName),
            typeof(string),
            typeof(ColorPickerBase),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorNamePropertyChanged));

    public static readonly DependencyProperty ColorNamesDictionaryProperty =
        DependencyProperty.Register(
            nameof(ColorNamesDictionary),
            typeof(Dictionary<Color, string>),
            typeof(ColorPickerBase),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ColorHelperProperty =
        DependencyProperty.Register(
            nameof(ColorHelper),
            typeof(ColorHelper),
            typeof(ColorPickerBase),
            new PropertyMetadata(null, OnUpdateColorName));

    public static readonly DependencyProperty AProperty =
        DependencyProperty.Register(nameof(A), typeof(byte), typeof(ColorPickerBase), new FrameworkPropertyMetadata((byte)255, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChannelChanged));

    public static readonly DependencyProperty RProperty =
        DependencyProperty.Register(nameof(R), typeof(byte), typeof(ColorPickerBase), new FrameworkPropertyMetadata((byte)0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChannelChanged));

    public static readonly DependencyProperty GProperty =
        DependencyProperty.Register(nameof(G), typeof(byte), typeof(ColorPickerBase), new FrameworkPropertyMetadata((byte)0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChannelChanged));

    public static readonly DependencyProperty BProperty =
        DependencyProperty.Register(nameof(B), typeof(byte), typeof(ColorPickerBase), new FrameworkPropertyMetadata((byte)0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChannelChanged));

    public static readonly DependencyProperty HueProperty =
        DependencyProperty.Register(nameof(Hue), typeof(double), typeof(ColorPickerBase), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHsvValuesChanged));

    public static readonly DependencyProperty SaturationProperty =
        DependencyProperty.Register(nameof(Saturation), typeof(double), typeof(ColorPickerBase), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHsvValuesChanged));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(ColorPickerBase), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHsvValuesChanged));

    public static readonly DependencyProperty LabelAlphaChannelProperty =
        DependencyProperty.Register(nameof(LabelAlphaChannel), typeof(object), typeof(ColorPickerBase), new PropertyMetadata("A"));

    public static readonly DependencyProperty LabelRedChannelProperty =
        DependencyProperty.Register(nameof(LabelRedChannel), typeof(object), typeof(ColorPickerBase), new PropertyMetadata("R"));

    public static readonly DependencyProperty LabelGreenChannelProperty =
        DependencyProperty.Register(nameof(LabelGreenChannel), typeof(object), typeof(ColorPickerBase), new PropertyMetadata("G"));

    public static readonly DependencyProperty LabelBlueChannelProperty =
        DependencyProperty.Register(nameof(LabelBlueChannel), typeof(object), typeof(ColorPickerBase), new PropertyMetadata("B"));

    public static readonly DependencyProperty LabelColorPreviewProperty =
        DependencyProperty.Register(nameof(LabelColorPreview), typeof(object), typeof(ColorPickerBase), new PropertyMetadata("Preview"));

    public static readonly DependencyProperty LabelHueChannelProperty =
        DependencyProperty.Register(nameof(LabelHueChannel), typeof(object), typeof(ColorPickerBase), new PropertyMetadata("H"));

    public static readonly DependencyProperty LabelSaturationChannelProperty =
        DependencyProperty.Register(nameof(LabelSaturationChannel), typeof(object), typeof(ColorPickerBase), new PropertyMetadata("S"));

    public static readonly DependencyProperty LabelValueChannelProperty =
        DependencyProperty.Register(nameof(LabelValueChannel), typeof(object), typeof(ColorPickerBase), new PropertyMetadata("V"));

    public static readonly DependencyProperty LabelColorNameProperty =
        DependencyProperty.Register(nameof(LabelColorName), typeof(object), typeof(ColorPickerBase), new PropertyMetadata("Name"));

    public static readonly DependencyProperty AreRgbChannelsVisibleProperty =
        DependencyProperty.Register(nameof(AreRgbChannelsVisible), typeof(bool), typeof(ColorPickerBase), new PropertyMetadata(true));

    public static readonly DependencyProperty AreHsvChannelsVisibleProperty =
        DependencyProperty.Register(nameof(AreHsvChannelsVisible), typeof(bool), typeof(ColorPickerBase), new PropertyMetadata(true));

    public static readonly DependencyProperty IsAlphaChannelVisibleProperty =
        DependencyProperty.Register(nameof(IsAlphaChannelVisible), typeof(bool), typeof(ColorPickerBase), new PropertyMetadata(true, OnUpdateColorName));

    public static readonly DependencyProperty IsColorNameVisibleProperty =
        DependencyProperty.Register(nameof(IsColorNameVisible), typeof(bool), typeof(ColorPickerBase), new PropertyMetadata(true));

    public static readonly DependencyProperty IsEyeDropperVisibleProperty =
        DependencyProperty.Register(nameof(IsEyeDropperVisible), typeof(bool), typeof(ColorPickerBase), new PropertyMetadata(true));

    public static readonly RoutedEvent SelectedColorChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(SelectedColorChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<Color?>),
            typeof(ColorPickerBase));

    public Color? SelectedColor
    {
        get => (Color?)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public Color? DefaultColor
    {
        get => (Color?)GetValue(DefaultColorProperty);
        set => SetValue(DefaultColorProperty, value);
    }

    public HsvColor SelectedHsvColor => (HsvColor)GetValue(SelectedHsvColorProperty);

    public string? ColorName
    {
        get => (string?)GetValue(ColorNameProperty);
        set => SetValue(ColorNameProperty, value);
    }

    public Dictionary<Color, string>? ColorNamesDictionary
    {
        get => (Dictionary<Color, string>?)GetValue(ColorNamesDictionaryProperty);
        set => SetValue(ColorNamesDictionaryProperty, value);
    }

    public ColorHelper? ColorHelper
    {
        get => (ColorHelper?)GetValue(ColorHelperProperty);
        set => SetValue(ColorHelperProperty, value);
    }

    public byte A
    {
        get => (byte)GetValue(AProperty);
        set => SetValue(AProperty, value);
    }

    public byte R
    {
        get => (byte)GetValue(RProperty);
        set => SetValue(RProperty, value);
    }

    public byte G
    {
        get => (byte)GetValue(GProperty);
        set => SetValue(GProperty, value);
    }

    public byte B
    {
        get => (byte)GetValue(BProperty);
        set => SetValue(BProperty, value);
    }

    public double Hue
    {
        get => (double)GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public double Saturation
    {
        get => (double)GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public object LabelAlphaChannel
    {
        get => GetValue(LabelAlphaChannelProperty);
        set => SetValue(LabelAlphaChannelProperty, value);
    }

    public object LabelRedChannel
    {
        get => GetValue(LabelRedChannelProperty);
        set => SetValue(LabelRedChannelProperty, value);
    }

    public object LabelGreenChannel
    {
        get => GetValue(LabelGreenChannelProperty);
        set => SetValue(LabelGreenChannelProperty, value);
    }

    public object LabelBlueChannel
    {
        get => GetValue(LabelBlueChannelProperty);
        set => SetValue(LabelBlueChannelProperty, value);
    }

    public object LabelColorPreview
    {
        get => GetValue(LabelColorPreviewProperty);
        set => SetValue(LabelColorPreviewProperty, value);
    }

    public object LabelHueChannel
    {
        get => GetValue(LabelHueChannelProperty);
        set => SetValue(LabelHueChannelProperty, value);
    }

    public object LabelSaturationChannel
    {
        get => GetValue(LabelSaturationChannelProperty);
        set => SetValue(LabelSaturationChannelProperty, value);
    }

    public object LabelValueChannel
    {
        get => GetValue(LabelValueChannelProperty);
        set => SetValue(LabelValueChannelProperty, value);
    }

    public object LabelColorName
    {
        get => GetValue(LabelColorNameProperty);
        set => SetValue(LabelColorNameProperty, value);
    }

    public bool AreRgbChannelsVisible
    {
        get => (bool)GetValue(AreRgbChannelsVisibleProperty);
        set => SetValue(AreRgbChannelsVisibleProperty, value);
    }

    public bool AreHsvChannelsVisible
    {
        get => (bool)GetValue(AreHsvChannelsVisibleProperty);
        set => SetValue(AreHsvChannelsVisibleProperty, value);
    }

    public bool IsAlphaChannelVisible
    {
        get => (bool)GetValue(IsAlphaChannelVisibleProperty);
        set => SetValue(IsAlphaChannelVisibleProperty, value);
    }

    public bool IsColorNameVisible
    {
        get => (bool)GetValue(IsColorNameVisibleProperty);
        set => SetValue(IsColorNameVisibleProperty, value);
    }

    public bool IsEyeDropperVisible
    {
        get => (bool)GetValue(IsEyeDropperVisibleProperty);
        set => SetValue(IsEyeDropperVisibleProperty, value);
    }

    public event RoutedPropertyChangedEventHandler<Color?> SelectedColorChanged
    {
        add => AddHandler(SelectedColorChangedEvent, value);
        remove => RemoveHandler(SelectedColorChangedEvent, value);
    }

    internal virtual void OnSelectedColorChanged(Color? oldValue, Color? newValue)
    {
        SetCurrentValue(ColorNameProperty, ActiveColorHelper.GetColorName(newValue, ColorNamesDictionary, IsAlphaChannelVisible));

        if (newValue is { } color)
        {
            if (UpdateHsvValues)
            {
                var hsv = new HsvColor(color);
                SetCurrentValue(HueProperty, hsv.Hue);
                SetCurrentValue(SaturationProperty, hsv.Saturation);
                SetCurrentValue(ValueProperty, hsv.Value);
            }

            SetValue(SelectedHsvColorPropertyKey, new HsvColor(A / 255d, Hue, Saturation, Value));
            SetCurrentValue(AProperty, color.A);
            SetCurrentValue(RProperty, color.R);
            SetCurrentValue(GProperty, color.G);
            SetCurrentValue(BProperty, color.B);
        }

        RaiseEvent(new RoutedPropertyChangedEventArgs<Color?>(oldValue, newValue, SelectedColorChangedEvent));
    }

    private ColorHelper ActiveColorHelper => ColorHelper ?? ColorHelper.DefaultInstance;

    private static void OnSelectedColorPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is ColorPickerBase colorPicker && e.OldValue != e.NewValue && !colorPicker.ColorIsUpdating)
        {
            colorPicker.ColorIsUpdating = true;
            try
            {
                colorPicker.OnSelectedColorChanged(e.OldValue as Color?, e.NewValue as Color? ?? colorPicker.DefaultColor);
            }
            finally
            {
                colorPicker.ColorIsUpdating = false;
            }
        }
    }

    private static object? CoerceSelectedColor(DependencyObject dependencyObject, object? baseValue)
    {
        if (dependencyObject is ColorPickerBase colorPicker)
        {
            baseValue ??= colorPicker.DefaultColor;
        }

        return baseValue;
    }

    private static void OnDefaultColorPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is ColorPickerBase colorPicker && e.OldValue != e.NewValue)
        {
            colorPicker.SetCurrentValue(SelectedColorProperty, e.NewValue ?? colorPicker.SelectedColor);
        }
    }

    private static void OnColorNamePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ColorPickerBase colorPicker || colorPicker.ColorIsUpdating)
        {
            return;
        }

        if (string.IsNullOrEmpty(e.NewValue?.ToString()))
        {
            colorPicker.SetCurrentValue(SelectedColorProperty, null);
            return;
        }

        if (colorPicker.ActiveColorHelper.ColorFromString(e.NewValue?.ToString(), colorPicker.ColorNamesDictionary) is { } color)
        {
            if (colorPicker.SelectedColor != color)
            {
                colorPicker.SetCurrentValue(SelectedColorProperty, color);
            }
            else
            {
                colorPicker.ColorIsUpdating = true;
                try
                {
                    colorPicker.SetCurrentValue(ColorNameProperty, colorPicker.ActiveColorHelper.GetColorName(color, colorPicker.ColorNamesDictionary, colorPicker.IsAlphaChannelVisible));
                }
                finally
                {
                    colorPicker.ColorIsUpdating = false;
                }
            }

            return;
        }

        throw new InvalidCastException("Cannot convert the given input to a valid color.");
    }

    private static void OnUpdateColorName(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ColorPickerBase colorPicker)
        {
            return;
        }

        colorPicker.ColorIsUpdating = true;
        try
        {
            colorPicker.SetCurrentValue(ColorNameProperty, colorPicker.ActiveColorHelper.GetColorName(colorPicker.SelectedColor, colorPicker.ColorNamesDictionary, colorPicker.IsAlphaChannelVisible));
        }
        finally
        {
            colorPicker.ColorIsUpdating = false;
        }
    }

    private static void OnHsvValuesChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ColorPickerBase colorPicker || colorPicker.ColorIsUpdating)
        {
            return;
        }

        var hsv = new HsvColor(colorPicker.A / 255d, colorPicker.Hue, colorPicker.Saturation, colorPicker.Value);
        colorPicker.UpdateHsvValues = false;
        try
        {
            colorPicker.SetCurrentValue(SelectedColorProperty, hsv.ToColor());
            colorPicker.SetValue(SelectedHsvColorPropertyKey, hsv);
        }
        finally
        {
            colorPicker.UpdateHsvValues = true;
        }
    }

    private static void OnColorChannelChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is ColorPickerBase colorPicker && !colorPicker.ColorIsUpdating)
        {
            colorPicker.SetCurrentValue(SelectedColorProperty, Color.FromArgb(colorPicker.A, colorPicker.R, colorPicker.G, colorPicker.B));
        }
    }
}

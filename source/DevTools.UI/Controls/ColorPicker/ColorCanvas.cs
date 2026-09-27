using System.Windows;
using System.Windows.Input;
// ReSharper disable once CheckNamespace
namespace DevTools.UI.Controls;

/// <summary>
/// Saturation/value surface plus channel editors. Adapted from MahApps.Metro.
/// </summary>
[TemplatePart(Name = PartSaturationValueBox, Type = typeof(FrameworkElement))]
[TemplatePart(Name = PartColorEyeDropper, Type = typeof(ColorEyeDropper))]
public class ColorCanvas : ColorPickerBase
{
    internal const string PartColorEyeDropper = "PART_ColorEyeDropper";

    private FrameworkElement? _saturationValueBox;

    static ColorCanvas()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorCanvas), new FrameworkPropertyMetadata(typeof(ColorCanvas)));
    }

    public override void OnApplyTemplate()
    {
        if (_saturationValueBox is not null)
        {
            _saturationValueBox.MouseLeftButtonDown -= OnSaturationMouseDown;
            _saturationValueBox.MouseLeftButtonUp -= OnSaturationMouseUp;
        }

        base.OnApplyTemplate();
        _saturationValueBox = GetTemplateChild(PartSaturationValueBox) as FrameworkElement;
        if (_saturationValueBox is not null)
        {
            _saturationValueBox.MouseLeftButtonDown += OnSaturationMouseDown;
            _saturationValueBox.MouseLeftButtonUp += OnSaturationMouseUp;
        }
    }

    private void OnSaturationMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_saturationValueBox is null)
        {
            return;
        }

        _saturationValueBox.ReleaseMouseCapture();
        _saturationValueBox.MouseMove -= OnSaturationMouseMove;
    }

    private void OnSaturationMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_saturationValueBox is null)
        {
            return;
        }

        Mouse.Capture(_saturationValueBox);
        _saturationValueBox.MouseMove += OnSaturationMouseMove;
        UpdateValues(e.GetPosition(_saturationValueBox));
    }

    private void OnSaturationMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _saturationValueBox is not null)
        {
            UpdateValues(e.GetPosition(_saturationValueBox));
        }
    }

    private void UpdateValues(Point position)
    {
        if (_saturationValueBox is null || _saturationValueBox.ActualWidth < 1 || _saturationValueBox.ActualHeight < 1)
        {
            return;
        }

        var saturation = position.X / _saturationValueBox.ActualWidth;
        var value = 1 - (position.Y / _saturationValueBox.ActualHeight);
        SetCurrentValue(SaturationProperty, ClampUnit(saturation));
        SetCurrentValue(ValueProperty, ClampUnit(value));
    }

    private static double ClampUnit(double value)
    {
        if (value < 0)
        {
            return 0;
        }

        return value > 1 ? 1 : value;
    }
}

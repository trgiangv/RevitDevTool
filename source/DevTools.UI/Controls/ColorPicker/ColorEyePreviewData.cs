using System.Windows;
using System.Windows.Media;
// ReSharper disable once CheckNamespace
namespace DevTools.UI.Controls;

/// <summary>
/// Live preview shown while <see cref="ColorEyeDropper"/> is dragging. Adapted from MahApps.Metro.
/// </summary>
public class ColorEyePreviewData : DependencyObject
{
    internal static readonly DependencyPropertyKey PreviewImagePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PreviewImage),
            typeof(ImageSource),
            typeof(ColorEyePreviewData),
            new PropertyMetadata(default(ImageSource)));

    public static readonly DependencyProperty PreviewImageProperty = PreviewImagePropertyKey.DependencyProperty;

    internal static readonly DependencyPropertyKey PreviewBrushPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PreviewBrush),
            typeof(Brush),
            typeof(ColorEyePreviewData),
            new PropertyMetadata(Brushes.Transparent));

    public static readonly DependencyProperty PreviewBrushProperty = PreviewBrushPropertyKey.DependencyProperty;

    public ImageSource? PreviewImage => (ImageSource?)GetValue(PreviewImageProperty);

    public Brush PreviewBrush => (Brush)GetValue(PreviewBrushProperty);
}

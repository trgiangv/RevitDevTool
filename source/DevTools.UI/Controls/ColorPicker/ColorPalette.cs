using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
// ReSharper disable once CheckNamespace
namespace DevTools.UI.Controls;

/// <summary>
/// A wrapping list of color swatches. Adapted from MahApps.Metro.
/// </summary>
public class ColorPalette : ListBox
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(object), typeof(ColorPalette), new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderTemplateProperty =
        DependencyProperty.Register(nameof(HeaderTemplate), typeof(DataTemplate), typeof(ColorPalette), new PropertyMetadata(null));

    public static readonly DependencyProperty ColorNamesDictionaryProperty =
        DependencyProperty.Register(nameof(ColorNamesDictionary), typeof(Dictionary<Color, string>), typeof(ColorPalette), new PropertyMetadata(null));

    public static readonly DependencyProperty IsAlphaChannelVisibleProperty =
        DependencyProperty.Register(nameof(IsAlphaChannelVisible), typeof(bool), typeof(ColorPalette), new PropertyMetadata(true));

    public static readonly DependencyProperty ColorHelperProperty =
        DependencyProperty.Register(nameof(ColorHelper), typeof(ColorHelper), typeof(ColorPalette), new PropertyMetadata(null));

    static ColorPalette()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorPalette), new FrameworkPropertyMetadata(typeof(ColorPalette)));
    }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public DataTemplate? HeaderTemplate
    {
        get => (DataTemplate?)GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public Dictionary<Color, string>? ColorNamesDictionary
    {
        get => (Dictionary<Color, string>?)GetValue(ColorNamesDictionaryProperty);
        set => SetValue(ColorNamesDictionaryProperty, value);
    }

    public bool IsAlphaChannelVisible
    {
        get => (bool)GetValue(IsAlphaChannelVisibleProperty);
        set => SetValue(IsAlphaChannelVisibleProperty, value);
    }

    public ColorHelper? ColorHelper
    {
        get => (ColorHelper?)GetValue(ColorHelperProperty);
        set => SetValue(ColorHelperProperty, value);
    }

    internal bool FocusSelectedItem()
    {
        ListBoxItem? listBoxItem = null;
        if (SelectedIndex >= 0)
        {
            listBoxItem = ItemContainerGenerator.ContainerFromIndex(SelectedIndex) as ListBoxItem;
        }
        else if (Items.Count > 0)
        {
            listBoxItem = ItemContainerGenerator.ContainerFromItem(Items[0]) as ListBoxItem;
        }

        return listBoxItem is not null && listBoxItem.Focus();
    }
}

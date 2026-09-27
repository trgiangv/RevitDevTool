using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
// ReSharper disable once CheckNamespace
namespace DevTools.UI.Controls;

/// <summary>
/// Built-in swatch lists for <see cref="ColorPicker"/>. Adapted from MahApps.Metro <c>BuildInColorPalettes</c>.
/// </summary>
public static class BuiltInColorPalettes
{
    public static Color[] StandardColorsPalette { get; } =
    [
        Colors.Transparent,
        Colors.White,
        Colors.LightGray,
        Colors.Gray,
        Colors.Black,
        Colors.DarkRed,
        Colors.Red,
        Colors.Orange,
        Colors.Brown,
        Colors.Yellow,
        Colors.LimeGreen,
        Colors.Green,
        Colors.DarkTurquoise,
        Colors.Aqua,
        Colors.Navy,
        Colors.Blue,
        Colors.Indigo,
        Colors.Purple,
        Colors.Fuchsia
    ];

    public static ObservableCollection<Color> WpfColorsPalette { get; } = new(
        typeof(Colors)
            .GetProperties()
            .Where(x => x.PropertyType == typeof(Color))
            .Select(x => (Color)(x.GetValue(null) ?? default(Color)))
            .OrderBy(c => new HsvColor(c).Hue)
            .ThenBy(c => new HsvColor(c).Saturation)
            .ThenByDescending(c => new HsvColor(c).Value));

    public static ObservableCollection<Color?> RecentColors { get; } = new();

    public static readonly DependencyProperty MaximumRecentColorsCountProperty =
        DependencyProperty.RegisterAttached(
            "MaximumRecentColorsCount",
            typeof(int),
            typeof(BuiltInColorPalettes),
            new PropertyMetadata(10));

    public static void AddColorToRecentColors(Color? color, IEnumerable? recentColors, int maxCount)
    {
        if (recentColors is not ObservableCollection<Color?> collection || maxCount < 1)
        {
            return;
        }

        var oldIndex = collection.IndexOf(color);
        if (oldIndex > 0)
        {
            collection.Move(oldIndex, 0);
        }
        else if (oldIndex < 0)
        {
            if (collection.Count >= maxCount)
            {
                collection.RemoveAt(maxCount - 1);
            }

            collection.Insert(0, color);
        }
    }

    [AttachedPropertyBrowsableForType(typeof(ColorPickerBase))]
    public static int GetMaximumRecentColorsCount(DependencyObject obj)
    {
        return (int)obj.GetValue(MaximumRecentColorsCountProperty);
    }

    [AttachedPropertyBrowsableForType(typeof(ColorPickerBase))]
    public static void SetMaximumRecentColorsCount(DependencyObject obj, int value)
    {
        obj.SetValue(MaximumRecentColorsCountProperty, value);
    }
}

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows.Media;
// ReSharper disable once CheckNamespace
namespace DevTools.UI.Controls;

/// <summary>
/// Converts between <see cref="Color"/>, hex, and named colors. Adapted from MahApps.Metro.
/// Named colors come from <see cref="Colors"/> unless a resource set is supplied.
/// </summary>
public class ColorHelper
{
    public static readonly ColorHelper DefaultInstance = new();

    public ColorHelper()
    {
        ColorNamesDictionary = CreateDictionaryFromColors();
    }

    public ColorHelper(CultureInfo? culture, Type? resourceDictionaryType)
    {
        if (culture is null || resourceDictionaryType is null)
        {
            ColorNamesDictionary = CreateDictionaryFromColors();
            return;
        }

        ColorNamesDictionary = new Dictionary<Color, string>();
        var resourceSet = new ResourceManager(resourceDictionaryType).GetResourceSet(culture, true, true);
        if (resourceSet is null)
        {
            return;
        }

        foreach (var entry in resourceSet.OfType<DictionaryEntry>())
        {
            try
            {
                if (ColorConverter.ConvertFromString(entry.Key.ToString()) is Color color)
                {
                    ColorNamesDictionary.Add(color, entry.Value?.ToString() ?? string.Empty);
                }
            }
            catch (Exception)
            {
                Trace.TraceError($"{entry.Key} is not a valid color key.");
            }
        }
    }

    public Dictionary<Color, string> ColorNamesDictionary { get; }

    public virtual Color? ColorFromString(string? colorName, Dictionary<Color, string>? colorNamesDictionary)
    {
        Color? result = null;

        try
        {
            if (string.IsNullOrWhiteSpace(colorName))
            {
                return null;
            }

            colorNamesDictionary ??= ColorNamesDictionary;

            if (!colorName.StartsWith("#", StringComparison.Ordinal)
                && colorNamesDictionary.FirstOrDefault(x => string.Equals(x.Value, colorName, StringComparison.OrdinalIgnoreCase)) is { } match
                && !string.IsNullOrEmpty(match.Value))
            {
                result = match.Key;
            }

            result ??= ColorConverter.ConvertFromString(colorName) as Color?;
        }
        catch (FormatException)
        {
            if (colorName is not null && !result.HasValue && !colorName.StartsWith("#", StringComparison.Ordinal))
            {
                result = ColorFromString("#" + colorName);
            }
        }

        return result;
    }

    public Color? ColorFromString(string? colorName)
    {
        return ColorFromString(colorName, null);
    }

    public virtual string? GetColorName(Color? color, Dictionary<Color, string>? colorNamesDictionary, bool useAlphaChannel)
    {
        if (color is null)
        {
            return null;
        }

        colorNamesDictionary ??= ColorNamesDictionary;
        var value = color.Value;
        var colorHex = useAlphaChannel ? value.ToString() : $"#{value.R:X2}{value.G:X2}{value.B:X2}";
        return colorNamesDictionary.TryGetValue(value, out var name) ? $"{name} ({colorHex})" : colorHex;
    }

    public string? GetColorName(Color? color)
    {
        return GetColorName(color, null, true);
    }

    private static Dictionary<Color, string> CreateDictionaryFromColors()
    {
        var dictionary = new Dictionary<Color, string>();
        foreach (var propertyInfo in typeof(Colors).GetProperties(BindingFlags.Static | BindingFlags.Public))
        {
            try
            {
                var color = (Color)(propertyInfo.GetValue(null) ?? default(Color));
                if (!dictionary.ContainsKey(color))
                {
                    dictionary.Add(color, propertyInfo.Name);
                }
            }
            catch (Exception)
            {
                Trace.TraceError($"Color from {propertyInfo.Name} could not be added to the dictionary.");
            }
        }

        return dictionary;
    }
}

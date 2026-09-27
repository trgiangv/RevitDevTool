using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
// ReSharper disable ConvertToExtensionBlock

namespace DevTools.UI.Theme;

/// <summary>
/// Resource helper class for loading ResourceDictionaries compiled into this assembly.
/// </summary>
public static class ResourceUtils
{
    private static ResourceDictionary? handyControls;
    private static ResourceDictionary? handyLightTheme;
    private static ResourceDictionary? handyDarkTheme;

    private static ResourceDictionary GetResource(string resourcePath)
    {
        var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        return new ResourceDictionary
        {
            Source = new Uri($"/{assemblyName};component/{resourcePath}", UriKind.RelativeOrAbsolute)
        };
    }

    public static ResourceDictionary GetHandyControls()
    {
        return handyControls ??= GetResource("Themes/Theme.xaml");
    }

    public static ResourceDictionary GetHandyLightTheme()
    {
        return handyLightTheme ??= GetResource("Themes/SkinDefault.xaml");
    }

    public static ResourceDictionary GetHandyDarkTheme()
    {
        return handyDarkTheme ??= GetResource("Themes/SkinDark.xaml");
    }

    public static void RemoveIfNotNull(this Collection<ResourceDictionary> mergedDictionaries, ResourceDictionary? item)
    {
        if (item != null)
        {
            mergedDictionaries.Remove(item);
        }
    }

    public static void InsertOrReplace(this Collection<ResourceDictionary> mergedDictionaries, int index, ResourceDictionary item)
    {
        if (mergedDictionaries.Count > index)
        {
            mergedDictionaries[index] = item;
        }
        else
        {
            mergedDictionaries.Insert(index, item);
        }
    }

    public static void RemoveAll<T>(this Collection<ResourceDictionary> mergedDictionaries) where T : ResourceDictionary
    {
        for (var i = mergedDictionaries.Count - 1; i >= 0; i--)
        {
            if (mergedDictionaries[i] is T)
            {
                mergedDictionaries.RemoveAt(i);
            }
        }
    }

    public static void SealValues(this ResourceDictionary dictionary)
    {
        foreach (var md in dictionary.MergedDictionaries)
        {
            md.SealValues();
        }

        foreach (var value in dictionary.Values)
        {
            SealValue(value);
        }
    }

    private static void SealValue(object value)
    {
        switch (value)
        {
            case Freezable freezable:
                SealFreezable(freezable);
                break;
            case Style { IsSealed: false } style:
                style.Seal();
                break;
        }
    }

    private static void SealFreezable(Freezable freezable)
    {
        if (!freezable.CanFreeze)
        {
            ResolveFreezableExpressions(freezable);
        }

        if (!freezable.IsFrozen)
        {
            freezable.Freeze();
        }
    }

    private static void ResolveFreezableExpressions(Freezable freezable)
    {
        var enumerator = freezable.GetLocalValueEnumerator();
        while (enumerator.MoveNext())
        {
            var property = enumerator.Current.Property;
            if (DependencyPropertyHelper.GetValueSource(freezable, property).IsExpression)
            {
                freezable.SetValue(property, freezable.GetValue(property));
            }
        }
    }
}

using System.Collections.ObjectModel;
using System.Windows;
// ReSharper disable ConvertToExtensionBlock

namespace DevTools.Utilities;

/// <summary>
/// Resource helper class for loading ResourceDictionaries.
/// Handles URI resolution for both standalone and ILRepack merged assemblies.
/// </summary>
public static class ResourceUtils
{
    private static ResourceDictionary? _mahAppsControls;
    private static ResourceDictionary? _mahAppsLightTheme;
    private static ResourceDictionary? _mahAppsDarkTheme;

    private static ResourceDictionary GetResource(string assemblyName, string resourcePath)
    {
        var uri = new Uri($"pack://application:,,,/{assemblyName};component/{resourcePath}", UriKind.Absolute);
        return new ResourceDictionary { Source = uri };
    }

    public static ResourceDictionary GetMahAppsControls()
    {
        return _mahAppsControls ??= GetResource("DevTools.MahApps.Metro", "Styles/Controls.xaml");
    }

    public static ResourceDictionary GetMahAppsLightTheme()
    {
        return _mahAppsLightTheme ??= GetResource("DevTools.MahApps.Metro", "Styles/Themes/Light.Blue.xaml");
    }

    public static ResourceDictionary GetMahAppsDarkTheme()
    {
        return _mahAppsDarkTheme ??= GetResource("DevTools.MahApps.Metro", "Styles/Themes/Dark.Blue.xaml");
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

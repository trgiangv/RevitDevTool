using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
// ReSharper disable ConvertToExtensionBlock

namespace RevitDevTool.Utils;

/// <summary>
/// Resource helper class for loading ResourceDictionaries.
/// Handles URI resolution for both standalone and ILRepack merged assemblies.
/// </summary>
public static class ResourceUtils
{
    private const string MahAppsAssemblyName = "MahApps.Metro";
    private static ResourceDictionary? _mahAppsControls;
    private static ResourceDictionary? _mahAppsLightTheme;
    private static ResourceDictionary? _mahAppsDarkTheme;

    /// <summary>
    /// Loads a ResourceDictionary from the specified assembly and resource path.
    /// First tries the external assembly, then falls back to merged assembly path.
    /// </summary>
    private static ResourceDictionary GetResource(string assemblyName, string resourcePath)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/{assemblyName};component/{resourcePath}", UriKind.Absolute);
            return new ResourceDictionary { Source = uri };
        }
        catch
        {
            var executingAssembly = Assembly.GetExecutingAssembly().GetName().Name;
            var uri = new Uri($"pack://application:,,,/{executingAssembly};component/{assemblyName}/{resourcePath}", UriKind.Absolute);
            return new ResourceDictionary { Source = uri };
        }
    }

    /// <summary>
    /// Get MahApps.Metro controls dictionary.
    /// </summary>
    public static ResourceDictionary GetMahAppsControls()
    {
        return _mahAppsControls ??= GetResource(MahAppsAssemblyName, "Styles/Controls.xaml");
    }

    /// <summary>
    /// Get MahApps.Metro light theme resource dictionary.
    /// </summary>
    public static ResourceDictionary GetMahAppsLightTheme()
    {
        return _mahAppsLightTheme ??= GetResource(MahAppsAssemblyName, "Styles/Themes/Light.Blue.xaml");
    }

    /// <summary>
    /// Get MahApps.Metro dark theme resource dictionary.
    /// </summary>
    public static ResourceDictionary GetMahAppsDarkTheme()
    {
        return _mahAppsDarkTheme ??= GetResource(MahAppsAssemblyName, "Styles/Themes/Dark.Blue.xaml");
    }

    /// <summary>
    /// Removes the specified ResourceDictionary from the collection if it is not null.
    /// </summary>
    /// <param name="mergedDictionaries">resource dictionary collection</param>
    /// <param name="item">item to remove</param>
    public static void RemoveIfNotNull(this Collection<ResourceDictionary> mergedDictionaries, ResourceDictionary? item)
    {
        if (item != null)
        {
            mergedDictionaries.Remove(item);
        }
    }

    /// <summary>
    /// Inserts or replaces a ResourceDictionary at the specified index.
    /// </summary>
    /// <param name="mergedDictionaries">resource dictionary collection</param>
    /// <param name="index">index to insert or replace at</param>
    /// <param name="item">item to insert or replace</param>
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

    /// <summary>
    /// Removes all ResourceDictionary items of the specified type from the collection.
    /// Iterates in reverse order to safely remove items during enumeration.
    /// </summary>
    /// <typeparam name="T">The type of ResourceDictionary to remove.</typeparam>
    /// <param name="mergedDictionaries">The resource dictionary collection.</param>
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

    /// <summary>
    /// Recursively seals Freezable and Style values in the ResourceDictionary.
    /// </summary>
    /// <param name="dictionary">The ResourceDictionary to seal values in.</param>
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

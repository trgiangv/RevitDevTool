using System.Reflection;
using System.Windows;

namespace RevitDevTool.Utils;

/// <summary>
/// Resource helper class for loading ResourceDictionaries.
/// Handles URI resolution for both standalone and ILRepack merged assemblies.
/// </summary>
public static class ResourceHelper
{
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
            var uri = new Uri($"/{assemblyName};component/{resourcePath}", UriKind.RelativeOrAbsolute);
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
        return _mahAppsControls ??= GetResource("MahApps.Metro", "Styles/Controls.xaml");
    }

    /// <summary>
    /// Get MahApps.Metro light theme resource dictionary.
    /// </summary>
    public static ResourceDictionary GetMahAppsLightTheme()
    {
        return _mahAppsLightTheme ??= GetResource("MahApps.Metro", "Styles/Themes/Light.Blue.xaml");
    }

    /// <summary>
    /// Get MahApps.Metro dark theme resource dictionary.
    /// </summary>
    public static ResourceDictionary GetMahAppsDarkTheme()
    {
        return _mahAppsDarkTheme ??= GetResource("MahApps.Metro", "Styles/Themes/Dark.Blue.xaml");
    }
}

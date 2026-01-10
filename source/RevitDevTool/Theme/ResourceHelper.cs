using System.Reflection;
using System.Windows;

namespace RevitDevTool.Theme;

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
    public static ResourceDictionary GetResource(string assemblyName, string resourcePath)
    {
        // Try external assembly first (for non-merged scenario)
        try
        {
            var uri = new Uri($"/{assemblyName};component/{resourcePath}", UriKind.RelativeOrAbsolute);
            var dict = new ResourceDictionary { Source = uri };
            // Force load to verify it works
            _ = dict.Count;
            return dict;
        }
        catch
        {
            // Fallback: try loading from merged assembly using pack URI
            try
            {
                var executingAssembly = Assembly.GetExecutingAssembly().GetName().Name;
                var uri = new Uri($"pack://application:,,,/{executingAssembly};component/{assemblyName}/{resourcePath}", UriKind.Absolute);
                return new ResourceDictionary { Source = uri };
            }
            catch
            {
                // Last fallback: return empty dictionary
                return new ResourceDictionary();
            }
        }
    }

    /// <summary>
    /// Loads a ResourceDictionary using pack://application URI format.
    /// This is more reliable for external assemblies.
    /// </summary>
    public static ResourceDictionary GetExternalResource(string assemblyName, string resourcePath)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/{assemblyName};component/{resourcePath}", UriKind.Absolute);
            var dict = new ResourceDictionary { Source = uri };
            _ = dict.Count;
            return dict;
        }
        catch
        {
            return new ResourceDictionary();
        }
    }

    /// <summary>
    /// Get a ResourceDictionary from the RevitDevTool assembly.
    /// </summary>
    public static ResourceDictionary GetRevitDevToolResource(string resourcePath)
    {
        return GetResource("RevitDevTool", resourcePath);
    }

    /// <summary>
    /// Get MahApps.Metro controls dictionary.
    /// </summary>
    public static ResourceDictionary GetMahAppsControls()
    {
        return _mahAppsControls ??= GetExternalResource("MahApps.Metro", "Styles/Controls.xaml");
    }

    /// <summary>
    /// Get MahApps.Metro light theme resource dictionary.
    /// </summary>
    public static ResourceDictionary GetMahAppsLightTheme()
    {
        return _mahAppsLightTheme ??= GetExternalResource("MahApps.Metro", "Styles/Themes/Light.Blue.xaml");
    }

    /// <summary>
    /// Get MahApps.Metro dark theme resource dictionary.
    /// </summary>
    public static ResourceDictionary GetMahAppsDarkTheme()
    {
        return _mahAppsDarkTheme ??= GetExternalResource("MahApps.Metro", "Styles/Themes/Dark.Blue.xaml");
    }

    /// <summary>
    /// Get MahApps.Metro theme dictionary by theme type.
    /// </summary>
    public static ResourceDictionary GetMahAppsTheme(AppTheme theme)
    {
        return theme == AppTheme.Dark ? GetMahAppsDarkTheme() : GetMahAppsLightTheme();
    }
}

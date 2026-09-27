using System.Windows;
namespace DevTools.UI.Theme;

/// <summary>
/// Static ResourceDictionary for HandyControl themes and control styles.
/// </summary>
public class ControlsResources : ResourceDictionary
{
    private static ResourceDictionary? controls;

    public ControlsResources()
    {
        MergedDictionaries.Add(Controls);
    }

    /// <summary>
    /// Gets the static HandyControl resource dictionary (Themes/Theme.xaml).
    /// </summary>
    private static ResourceDictionary Controls => controls ??= ResourceUtils.GetHandyControls();
}

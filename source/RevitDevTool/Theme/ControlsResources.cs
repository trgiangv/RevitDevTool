using System.Windows;
using RevitDevTool.Utils;

namespace RevitDevTool.Theme;

/// <summary>
/// Static ResourceDictionary for MahApps.Metro controls.
/// </summary>
public class ControlsResources : ResourceDictionary
{
    private static ResourceDictionary? _controls;

    public ControlsResources()
    {
        MergedDictionaries.Add(Controls);
    }

    /// <summary>
    /// Gets the static MahApps.Metro controls resource dictionary.
    /// </summary>
    private static ResourceDictionary Controls => _controls ??= ResourceUtils.GetMahAppsControls();
}

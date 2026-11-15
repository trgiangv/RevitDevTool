using HandyControl.Controls;
using HandyControl.Interactivity;
using System.Windows;
using System.Windows.Input;
namespace DevTools.UI.Theme;

/// <summary>
/// Static ResourceDictionary for HandyControl themes and control styles.
/// </summary>
public class ControlsResources : ResourceDictionary
{
    private static ResourceDictionary? _controls;

    static ControlsResources()
    {
        CommandManager.RegisterClassCommandBinding(typeof(SearchBar), new CommandBinding(ControlCommands.Clear, OnSearchBarClear));
    }

    public ControlsResources()
    {
        MergedDictionaries.Add(Controls);
    }

    /// <summary>
    /// Gets the static HandyControl resource dictionary (Themes/Theme.xaml).
    /// </summary>
    private static ResourceDictionary Controls => _controls ??= ResourceUtils.GetHandyControls();

    private static void OnSearchBarClear(object sender, ExecutedRoutedEventArgs e)
    {
        if (sender is SearchBar { IsReadOnly: false } bar)
        {
            bar.Clear();
        }

        e.Handled = true;
    }
}

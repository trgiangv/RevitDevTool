using System.Windows;
using Wpf.Ui.Appearance;

namespace RevitDevTool.Theme;

public interface IThemeWatcherService
{
    void Initialize();
    void ApplyTheme();
    void Watch(FrameworkElement frameworkElement);
    void Unwatch();

    /// <summary>
    /// Returns the effective theme (never <see cref="ApplicationTheme.Auto"/>).
    /// </summary>
    ApplicationTheme GetEffectiveTheme();
}



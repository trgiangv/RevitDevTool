using System.ComponentModel;
using System.Windows;
namespace RevitDevTool.Theme.Design;

/// <summary>
/// Enables you to detect whether your app is in design mode in a visual designer.
/// </summary>
internal static class DesignMode
{
    private static readonly Lazy<bool> DesignModeEnabled =
        new(() => DesignerProperties.GetIsInDesignMode(new DependencyObject()));

    /// <summary>
    /// Gets a value that indicates whether the process is running in design mode.
    /// </summary>
    /// <returns><c>true</c> if the process is running in design mode; otherwise <c>false</c>.</returns>
    public static bool IsDesignModeEnabled => DesignModeEnabled.Value;
}

using System.IO;
using System.Reflection;
using System.Windows;
using RevitDevTool.Core;
using RevitDevTool.Core.DockableLoader;

namespace RevitDevTool.ExternalEvent.App;

/// <summary>
///     Registers the hot-reload dockable pane (collectible ALC + <see cref="DockablePaneProvider" /> factory path).
/// </summary>
internal static class HotReloadDockablePane
{
    internal static readonly Guid PaneGuid = new("A3F9E2C1-8B4D-4E6F-9A1C-2D5E8F0B7A3D");

    private const string PaneTitle = "Hot reload (ALC test)";

    private const string HotReloadRelativeFolder = "HotReload";
    private const string HotReloadAssemblyFileName = "RevitDevTool.ExternalEvent.HotReloadUi.dll";

    private const string HotReloadTypeFullName = "RevitDevTool.ExternalEvent.HotReloadUi.HotReloadPanePage";

    public static void Register(UIControlledApplication application)
    {
        DockablePaneProvider
            .Register(application, PaneGuid, PaneTitle)
            .SetFrameworkElementFactory(CreatePaneRoot)
            .SetConfiguration(data =>
            {
                data.InitialState = new DockablePaneState
                {
                    MinimumWidth = 340,
                    MinimumHeight = 220,
                    DockPosition = DockPosition.Right,
                    TabBehind = DockablePanes.BuiltInDockablePanes.PropertiesPalette
                };
            });
    }

    private static FrameworkElement CreatePaneRoot()
    {
        var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var satellitePath = Path.Combine(baseDir, HotReloadRelativeFolder, HotReloadAssemblyFileName);

        CollectibleAssemblyPane.PaneReload = HotReloadPaneSession.Reload;

        var host = CollectibleAssemblyPane.Load(satellitePath, HotReloadTypeFullName);
        HotReloadPaneSession.Attach(host, satellitePath, HotReloadTypeFullName);
        return host;
    }
}

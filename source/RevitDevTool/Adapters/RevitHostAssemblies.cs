using System.Reflection;
using DevTools.AssemblyIsolation;

namespace RevitDevTool.Adapters;

internal sealed class RevitHostAssemblies : HostAssemblies
{
    protected override IEnumerable<Assembly> LoadedByType
    {
        get
        {
            yield return typeof(Element).Assembly; // RevitAPI.dll
            yield return typeof(IExternalCommand).Assembly; // RevitAPIUI.dll
            yield return typeof(Autodesk.Windows.RibbonItem).Assembly; // Autodesk.Windows.dll
            yield return typeof(UIFramework.ApplicationTheme).Assembly; // UIFramework.dll
            yield return typeof(Autodesk.Windows.ToolBars.QuickAccessRedoButton).Assembly; // AdWindows.dll
            yield return typeof(UIFrameworkServices.QuickAccessToolBarService).Assembly; // UIFrameworkServices.dll
        }
    }

    protected override IReadOnlyList<string> LoadedByName { get; } =
    [
        "RevitAPIMacros",
        "RevitAPIIFC",
        "RevitAddinUtility",
        "RevitNET",
        "PackageContentParser"
    ];
}

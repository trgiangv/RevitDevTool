using System.Reflection;
namespace RevitDevTool.Adapters;

/// <summary>
/// Host Autodesk API assemblies already loaded by Revit. Each entry is a
/// compile-time type anchor — missing types fail at build, not at load.
/// </summary>
internal static class RevitHostApis
{
    internal static IEnumerable<Assembly> All()
    {
        yield return typeof(Element).Assembly;
        yield return typeof(IExternalCommand).Assembly;
        yield return typeof(Autodesk.Windows.RibbonItem).Assembly;
        yield return typeof(UIFramework.ApplicationTheme).Assembly;
        yield return typeof(UIFrameworkServices.QuickAccessToolBarService).Assembly;
    }
}

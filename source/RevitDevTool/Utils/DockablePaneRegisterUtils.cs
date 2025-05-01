using System.Windows.Controls;
using Autodesk.Revit.UI;

namespace RevitDevTool.Utils;

public static class DockablePaneRegisterUtils
{
    public static void Register<T>(string strGuid, UIControlledApplication application) where T : Page, IDockablePaneProvider, new()
    {
        var page = new T();
        var guid = new Guid(strGuid);
        var dockablePaneId = new DockablePaneId(guid);
        application.RegisterDockablePane(dockablePaneId, page.Title, (IDockablePaneProvider)page);
    }

    public static void Register<T>(string strGuid, UIApplication application) where T : Page, IDockablePaneProvider, new()
    {
        var page = new T();
        var guid = new Guid(strGuid);
        var dockablePaneId = new DockablePaneId(guid);
        application.RegisterDockablePane(dockablePaneId, page.Title, (IDockablePaneProvider)page);
    }
}
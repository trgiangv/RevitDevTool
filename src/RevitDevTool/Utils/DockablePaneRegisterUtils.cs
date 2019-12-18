using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Autodesk.Revit.UI;

namespace RevitDevTool.Utils
{
    public class DockablePaneRegisterUtils
    {
        public static void Register<T>(string strGuid, UIControlledApplication application) where T : Page, IDockablePaneProvider, new()
        {
            T page = new T();
            Guid guid = new Guid(strGuid);
            DockablePaneId dockablePaneId = new DockablePaneId(guid);
            application.RegisterDockablePane(dockablePaneId, page.Title, (IDockablePaneProvider)page);
        }

        public static void Register<T>(string strGuid, UIApplication application) where T : Page, IDockablePaneProvider, new()
        {
            T page = new T();
            Guid guid = new Guid(strGuid);
            DockablePaneId dockablePaneId = new DockablePaneId(guid);
            application.RegisterDockablePane(dockablePaneId, page.Title, (IDockablePaneProvider)page);
        }
    }
}

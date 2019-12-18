using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using RevitDevTool.View;

namespace RevitDevTool.Revit.Command
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class TraceCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            DockablePaneId dockablePaneId = new DockablePaneId(new Guid(Resource.TraceGuid));

            if (DockablePane.PaneIsRegistered(dockablePaneId))
            {
                if (commandData.Application.GetDockablePane(dockablePaneId).IsShown())
                {
                    commandData.Application.GetDockablePane(dockablePaneId).Hide();
                }
                else
                {
                    commandData.Application.GetDockablePane(dockablePaneId).Show();
                }
            }
            else
            {
                DockablePaneRegisterUtils.Register<TraceOutputPage>(Resource.TraceGuid, commandData.Application);
                commandData.Application.GetDockablePane(dockablePaneId).Show();
            }

            return Result.Succeeded;
        }
    }
}

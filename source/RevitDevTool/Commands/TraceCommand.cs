using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using RevitDevTool.View;
using DockablePaneProvider = RevitDevTool.Utils.DockablePaneProvider;

namespace RevitDevTool.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class TraceCommand : IExternalCommand
{
    private const string CommandName = "TraceLog";
    private const string Guid = "43AE2B41-0BE6-425A-B27A-724B2CE17351";
    
    public static void RegisterDockablePane(UIControlledApplication application)
    {
        DockablePaneProvider
            .Register(application, new Guid(Guid), CommandName)
            .SetConfiguration(data =>
            {
                data.FrameworkElement = new TraceLog();
                data.InitialState = new DockablePaneState
                {
                    MinimumWidth = 300,
                    MinimumHeight = 400,
                    DockPosition = DockPosition.Left,
                    TabBehind = DockablePanes.BuiltInDockablePanes.PropertiesPalette
                };
            });
    }

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var id = new DockablePaneId(new Guid(Guid));
            var dockableWindow = commandData.Application.GetDockablePane(id);
            if(!dockableWindow.IsShown())
                dockableWindow.Show();
            else
                dockableWindow.Hide();
            return Result.Succeeded;
        }
        catch (Exception e)
        {
            TaskDialog.Show("Error", e.ToString());
            return Result.Failed;
        }
    }
}
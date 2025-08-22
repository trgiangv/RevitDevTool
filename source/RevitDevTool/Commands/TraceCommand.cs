using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.Decorators;
using Nice3point.Revit.Toolkit.External;
using RevitDevTool.View;

namespace RevitDevTool.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class TraceCommand : ExternalCommand
{
    private const string CommandName = "TraceLog";
    private const string Guid = "43AE2B41-0BE6-425A-B27A-724B2CE17351";
    
    public override void Execute()
    {
        try
        {
            var id = new DockablePaneId(new Guid(Guid));
            var dockableWindow = UiApplication.GetDockablePane(id);
            if(!dockableWindow.IsShown())
                dockableWindow.Show();
            else
                dockableWindow.Hide();
        }
        catch (Exception e)
        {
            Autodesk.Revit.UI.TaskDialog.Show("Error", e.ToString());
        }
    }
    
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
                    DockPosition = DockPosition.Right,
                    TabBehind = DockablePanes.BuiltInDockablePanes.PropertiesPalette
                };
            });
    }
}
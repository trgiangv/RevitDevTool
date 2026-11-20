using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

[Transaction(TransactionMode.Manual)]
public class Command : IExternalCommand
{
    private const string PaneGuid = "6C1E3B7A-52D4-4F0B-9E57-2E8B1C4D9A31";
    private const bool Show = false;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var id = new DockablePaneId(new Guid(PaneGuid));
        if (!DockablePane.PaneIsRegistered(id) || !DockablePane.PaneExists(id))
        {
            message = "The Model Context pane is not registered or not created in this session.";
            return Result.Failed;
        }

        var pane = commandData.Application.GetDockablePane(id);
        if (Show)
        {
            pane.Show();
        }
        else
        {
            pane.Hide();
        }

        message = "Model Context pane " + (Show ? "shown" : "hidden") + "; IsShown = " + pane.IsShown() + ".";
        return Result.Succeeded;
    }
}

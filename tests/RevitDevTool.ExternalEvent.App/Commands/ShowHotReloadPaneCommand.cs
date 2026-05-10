using Autodesk.Revit.UI;

namespace RevitDevTool.ExternalEvent.App.Commands;

/// <summary>Shows the hot-reload test dockable pane.</summary>
[UsedImplicitly]
[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
internal sealed class ShowHotReloadPaneCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var pane = commandData.Application.GetDockablePane(new DockablePaneId(HotReloadDockablePane.PaneGuid));
            pane.Show();
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }

        return Result.Succeeded;
    }
}

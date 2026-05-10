using Autodesk.Revit.UI;

namespace RevitDevTool.ExternalEvent.App.Commands;

/// <summary>
///     Reloads pane content from the satellite HotReload\ DLL on disk (ribbon shortcut; same as the in-pane button).
/// </summary>
[UsedImplicitly]
[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
internal sealed class ReloadHotReloadPaneCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            HotReloadPaneSession.Reload();
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }

        return Result.Succeeded;
    }
}

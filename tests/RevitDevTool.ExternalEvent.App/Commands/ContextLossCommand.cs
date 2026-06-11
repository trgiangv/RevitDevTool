using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using TaskDialogIcon = Autodesk.Revit.UI.TaskDialogIcon;
namespace RevitDevTool.ExternalEvent.App.Commands;

[UsedImplicitly]
[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
internal class ContextLossCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var dialog = new TaskDialog("Choose Repro")
        {
            MainInstruction = "Select which repro to run:",
            CommonButtons = TaskDialogCommonButtons.Cancel,
            MainIcon = TaskDialogIcon.TaskDialogIconInformation,
        };
        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Safe: Re-enter API context before transaction");
        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Unsafe: Stay in same async delegate with API context loss");
        
        var result = dialog.Show();
        if (result == TaskDialogResult.CommandLink1)
        {
            _ = ContextLostRepro.Safe_AsyncDelegateOverload_ReenterBeforeWrite();
        }
        else if (result == TaskDialogResult.CommandLink2)
        {
            _ = ContextLostRepro.Unsafe_AsyncDelegateOverload_ContextLoss();
        }
        
        return Result.Succeeded;
    }
}

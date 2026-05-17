using Autodesk.Revit.Attributes;
using RevitDevTool.CommandBrowser;

namespace RevitDevTool.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CommandBrowserCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        Host.GetService<CommandBrowserController>().ToggleVisibility();
        return Result.Succeeded;
    }
}

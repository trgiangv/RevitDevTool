using Autodesk.Revit.Attributes;
using RevitDevTool.Tools.CommandBrowser;

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

    public static void Register(UIControlledApplication uiControlledApplication)
    {
        Host.GetService<CommandBrowserController>().Initialize(uiControlledApplication);
    }

    public static void Unregister()
    {
        Host.GetService<CommandBrowserController>().Shutdown();
    }
}

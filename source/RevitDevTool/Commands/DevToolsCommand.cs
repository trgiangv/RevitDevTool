using System.Windows.Input;
using Autodesk.Revit.Attributes;
using DevTools.Presentation.ViewModels;
using RevitDevTool.Controllers;

namespace RevitDevTool.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class DevToolsCommand : IExternalCommand, IExternalCommandAvailability
{
    public const string CommandName = "DevTools";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var panel = Host.GetService<PanelController>();

        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            if (PanelController.HasUiDocument)
                panel.TogglePaneVisibility();
            else
                panel.ToggleFloatingWindow();

            return Result.Succeeded;
        }

        Host.GetService<ExecutionViewModel>().ExecuteLastItem();
        return Result.Succeeded;
    }

    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
    {
        return true;
    }
}

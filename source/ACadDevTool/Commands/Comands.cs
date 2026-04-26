using AcadDevTool.View;
using AcadDevTool.ViewModel;
using Autodesk.AutoCAD.Runtime;
using DevTools.Utilities;
namespace AcadDevTool.Commands;

public static class Commands
{
    [CommandMethod("DevTools")]
    public static void DevToolsCommand()
    {
        Application.PanelController.ToggleVisibility();
    }

    [CommandMethod("StubBuilder")]
    public static void StubBuilderCommand()
    {
        var vm = new StubBuilderViewModel();
        var window = new StubBuilderWindow(vm);
        window.SetHostAppOwner();
        window.ShowDialog();
    }
}
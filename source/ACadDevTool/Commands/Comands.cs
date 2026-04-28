using AcadDevTool.Controllers;
using Autodesk.AutoCAD.Runtime;
using DevTools.Utilities;
using DevTools.Views.View;
using DevTools.Views.ViewModel;
namespace AcadDevTool.Commands;

public static class Commands
{
    [CommandMethod("DevTools")]
    public static void DevToolsCommand()
    {
        Host.GetService<PanelController>().ToggleVisibility();
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
using AcadDevTool.Controllers;
using AcadDevTool.HostAdapters;
using Autodesk.AutoCAD.Runtime;
using DevTools.UI;
using DevTools.Presentation.ViewModels;
using DevTools.Presentation.Views;
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
        var vm = new StubBuilderViewModel(AcadProductDetector.GetVersionNumber());
        var window = new StubBuilderWindow(vm);
        window.SetHostAppOwner();
        window.ShowDialog();
    }
}
using AcadDevTool.Composition;
using AcadDevTool.Controllers;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using DevTools.Utilities;
using DevTools.Utilities.AssemblyLoading;
using ricaun.AutoCAD.UI;
using Application = AcadDevTool.Application;
[assembly: ExtensionApplication(typeof(Application))]

namespace AcadDevTool;

public class Application : ExtensionApplication
{
    public override void OnStartup(RibbonControl ribbonControl)
    {
        AssemblyLoader.Initialize();
        HostSharedAssemblies.Use(AcadHostApiAssemblies.Names);
        Host.Start();
        Host.GetService<PanelController>().Initialize();
        AddButtons(ribbonControl);
    }

    public override void OnShutdown(RibbonControl ribbonControl)
    {
        Host.GetService<PanelController>().Shutdown();
        Host.Stop();
    }

    private static void AddButtons(RibbonControl ribbonControl)
    {
        var ribbon = ComponentManager.Ribbon;
        var addInsTab = ribbon.Tabs.FirstOrDefault(tab => tab.Title.Equals("Add-ins", StringComparison.OrdinalIgnoreCase))
                        ?? ribbonControl.CreateOrSelectTab("Add-ins");
        var ribbonPanel = ribbonControl.CreateOrSelectPanel(addInsTab.Id, "External Tools");

        ribbonPanel.CreateButton("DevTools")
            .SetCommand(Commands.Commands.DevToolsCommand)
            .SetLargeImage("/DevTools.UI;component/Resources/Icons/DevTools-32-Light.png")
            .SetToolTip("Execute last command\nCtrl + click to Show/Hide DevTools");

        ribbonPanel.CreateButton("StubBuilder")
            .SetCommand(Commands.Commands.StubBuilderCommand)
            .SetLargeImage("/DevTools.UI;component/Resources/Icons/StubBuilder-32-Light.png")
            .SetToolTip("Generate Python .pyi stub files from .NET assemblies");
    }
}

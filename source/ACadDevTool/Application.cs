using System.IO;
using AcadDevTool.Controllers;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.Utilities;
using DevTools.AssemblyIsolation.Loading;
using Microsoft.Extensions.Logging;
using ricaun.AutoCAD.UI;
using ZLogger;
using Application = AcadDevTool.Application;
[assembly: ExtensionApplication(typeof(Application))]

namespace AcadDevTool;

public class Application : ExtensionApplication
{
    private PermanentDirectoryAssemblyResolver? _assemblyResolver;

    public override void OnStartup(RibbonControl ribbonControl)
    {
        var addinContentsDirectory = Path.GetDirectoryName(typeof(Application).Assembly.Location)
            ?? throw new InvalidOperationException("Could not determine the AutoCAD add-in contents directory.");
        _assemblyResolver ??= PermanentDirectoryAssemblyResolver.Create(
            addinContentsDirectory,
            new PermanentAssemblyLoader(new AddinAssemblyIsolationDiagnosticSink()));
        _assemblyResolver.Register();
        Host.Start();
        Host.GetService<PanelController>().Initialize();
        AddButtons(ribbonControl);
    }

    public override void OnShutdown(RibbonControl ribbonControl)
    {
        Host.GetService<PanelController>().Shutdown();
        Host.Stop();
        _assemblyResolver?.Dispose();
        _assemblyResolver = null;
    }

    private sealed class AddinAssemblyIsolationDiagnosticSink : IAssemblyIsolationDiagnosticSink
    {
        public void Publish(AssemblyIsolationDiagnostic diagnostic)
        {
            Host.GetService<ILogger<Application>>().ZLogWarning(
                $"Assembly isolation diagnostic '{diagnostic.Code}': {diagnostic.Message}");
        }
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

using System.IO;
using AcadDevTool.Adapters;
using AcadDevTool.Controllers;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.Logging.Diagnostics;
using DevTools.Utilities;
using DevTools.AssemblyIsolation.Loading;
using Microsoft.Extensions.Logging;
using ricaun.AutoCAD.UI;
using ZLogger;
using Application = AcadDevTool.Application;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
[assembly: ExtensionApplication(typeof(Application))]

namespace AcadDevTool;

public class Application : ExtensionApplication
{
    private AssemblyLoader? _assemblyLoader;
    private StartupTrace? _startup;

    public override void OnStartup(RibbonControl ribbonControl)
    {
        var versionNumber = AcadProductDetector.GetVersionNumber();
        var logsDirectory = Path.Combine(AppUtils.GetContentRootPath(versionNumber), "Logs");
        var startup = StartupTrace.Begin(
            AcadProductDetector.Detect().ToString().ToLower(),
            AcadApp.Version.ToString(),
            Environment.ProcessId,
            logsDirectory);
        _startup = startup;

        try
        {
            var addinContentsDirectory = Path.GetDirectoryName(typeof(Application).Assembly.Location)
                ?? throw new InvalidOperationException("Could not determine the AutoCAD add-in contents directory.");
            _assemblyLoader ??= new AssemblyLoader(new AddinAssemblyIsolationDiagnosticSink());
            startup.Mark("AssemblyLoader.Register");
            _assemblyLoader.Register(addinContentsDirectory);
            startup.Mark("Host.Start");
            Host.Start();
            startup.Mark("PanelController.Initialize");
            Host.GetService<PanelController>().Initialize();
            startup.Mark("AddButtons");
            AddButtons(ribbonControl);
            startup.Dispose();
            _startup = null;
        }
        catch (System.Exception ex)
        {
            startup.Fail(ex);
            throw;
        }
    }

    public override void OnShutdown(RibbonControl ribbonControl)
    {
        _startup?.Dispose();
        _startup = null;
        try
        {
            Host.GetService<PanelController>().Shutdown();
            Host.Stop();
        }
        catch
        {
            // Host.Start never completed.
        }

        _assemblyLoader?.Dispose();
        _assemblyLoader = null;
    }

    private sealed class AddinAssemblyIsolationDiagnosticSink : IAssemblyIsolationDiagnosticSink
    {
        public void Publish(AssemblyIsolationDiagnostic diagnostic)
        {
            var message = $"Assembly isolation diagnostic '{diagnostic.Code}': {diagnostic.Message}";
            if (StartupTrace.Current is { } trace)
            {
                trace.Mark(message);
                return;
            }

            Host.GetService<ILogger<Application>>().ZLogWarning($"{message}");
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

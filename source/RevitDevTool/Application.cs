using System.IO;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Loading;
using DevTools.Hosting;
using DevTools.Logging.Diagnostics;
using DevTools.Utilities;
using Microsoft.Extensions.Logging;
using Nice3point.Revit.Extensions.UI;
using Autodesk.Revit.DB.Events;
using RevitDevTool.CommandBrowser;
using RevitDevTool.Commands;
using RevitDevTool.Controllers;
using ZLogger;

namespace RevitDevTool;

[UsedImplicitly]
public class Application : IExternalApplication
{
    private UIControlledApplication? _application;
    private AssemblyLoader? _assemblyLoader;
    private StartupTrace? _startup;

    public Result OnStartup(UIControlledApplication application)
    {
        _application = application;
        var controlled = application.ControlledApplication;
        var logsDirectory = Path.Combine(AppUtils.GetContentRootPath(controlled.VersionNumber), "Logs");
        var startup = StartupTrace.Begin(
            nameof(HostApp.Revit).ToLower(),
            controlled.VersionBuild,
            Environment.ProcessId,
            logsDirectory);
        _startup = startup;

        try
        {
            var addinContentsDirectory = Path.GetDirectoryName(typeof(Application).Assembly.Location)
                ?? throw new InvalidOperationException("Could not determine the Revit add-in contents directory.");
            _assemblyLoader ??= new AssemblyLoader(new AddinAssemblyIsolationDiagnosticSink());
            startup.Mark("AssemblyLoader.Register");
            _assemblyLoader.Register(addinContentsDirectory);
            startup.Mark("Host.Start");
            Host.Start();
            startup.Mark("AddButtons");
            AddButtons(application);
            application.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            startup.Fail(ex);
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        _startup?.Dispose();
        _startup = null;
        try
        {
            Host.GetService<CommandBrowserController>().Shutdown();
            Host.GetService<PanelController>().Shutdown();
            Host.Stop();
        }
        catch
        {
            // Host.Start never completed.
        }

        _assemblyLoader?.Dispose();
        _assemblyLoader = null;
        return Result.Succeeded;
    }

    private void OnApplicationInitialized(object? sender, ApplicationInitializedEventArgs e)
    {
        if (_application is null) return;
        try
        {
            _startup?.Mark("ApplicationInitialized");
            Host.GetService<PanelController>().Initialize(_application);
            Host.GetService<CommandBrowserController>().Initialize(_application);

            _startup?.Dispose();
            _startup = null;
        }
        catch (Exception ex)
        {
            _startup?.Fail(ex);
        }
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

    private static void AddButtons(UIControlledApplication application)
    {
        var panel = application.CreatePanel("External Tools");

        panel.AddPushButton<DevToolsCommand>(DevToolsCommand.CommandName)
            .AddShortcuts("AD")
            .SetAvailabilityController<DevToolsCommand>()
            .SetLargeImage("/DevTools.UI;component/Resources/Icons/DevTools-32-Light.png")
            .SetToolTip("Execute last command\nCtrl + click to Show/Hide DevTools");

        var stack = panel.AddStackPanel();

        stack.AddPushButton<StubBuilderCommand>("StubBuilder")
            .SetAvailabilityController<StubBuilderCommand>()
            .SetLargeImage("/DevTools.UI;component/Resources/Icons/StubBuilder-32-Light.png")
            .SetImage("/DevTools.UI;component/Resources/Icons/StubBuilder-16-Light.png")
            .SetToolTip("Generate Python .pyi stub files from .NET assemblies");

        stack.AddPushButton<CommandBrowserCommand>("Commands")
            .SetLargeImage("/DevTools.UI;component/Resources/Icons/Commands-32-Light.png")
            .SetImage("/DevTools.UI;component/Resources/Icons/Commands-16-Light.png")
            .SetToolTip("Search and run any Revit ribbon command");
    }
}

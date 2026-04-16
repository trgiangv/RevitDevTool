using System.Windows.Threading;
using DevTools.Utilities;
using Nice3point.Revit.Extensions.UI;
using Autodesk.Revit.DB.Events;
using RevitDevTool.Commands;
using RevitDevTool.Controllers;
using RevitDevTool.Core;
using RevitDevTool.Execution.Providers.Python;

namespace RevitDevTool;

[UsedImplicitly]
public class Application : IExternalApplication
{
    private UIControlledApplication? _application;

    public Result OnStartup(UIControlledApplication application)
    {
        _application = application;
        AssemblyLoader.Initialize();
        ExternalEventController.Register();
        Host.Start();
        AddButtons(application);
        application.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        Host.GetService<PanelController>().Shutdown();
        application.ControlledApplication.ApplicationInitialized -= OnApplicationInitialized;
        ExecuteAsync(() => Host.GetService<PythonInitializer>().Shutdown());
        Host.Stop();
        return Result.Succeeded;
    }

    private void OnApplicationInitialized(object? sender, ApplicationInitializedEventArgs e)
    {
        if (_application is null) return;
        Host.GetService<PanelController>().Initialize(_application);
        ExecuteAsync(() => Host.GetService<PythonInitializer>().InitializeAsync());
    }

    private static void ExecuteAsync(Func<Task> asyncMethod)
    {
        var task = asyncMethod();
        if (task.IsCompleted)
        {
            task.GetAwaiter().GetResult();
            return;
        }

        var frame = new DispatcherFrame();
        task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }

    private static void AddButtons(UIControlledApplication application)
    {
        var panel = application.CreatePanel("External Tools");

        panel.AddPushButton<DevToolsCommand>(DevToolsCommand.CommandName)
            .AddShortcuts("AD")
            .SetAvailabilityController<DevToolsCommand>()
            .SetLargeImage("/RevitDevTool;component/Resources/Icons/TraceGeometry32_light.tiff")
            .SetToolTip("Execute last command\nCtrl + click to Show/Hide DevTools");

        panel.AddPushButton<StubBuilderCommand>("StubBuilder")
            .SetAvailabilityController<StubBuilderCommand>()
            .SetLargeImage("/RevitDevTool;component/Resources/Icons/python32.png")
            .SetToolTip("Generate Python .pyi stub files from .NET assemblies");
    }
}

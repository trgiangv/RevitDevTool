using Nice3point.Revit.Extensions.UI;
using Nice3point.Revit.Toolkit.External;
using Revit.Async;
using RevitDevTool.ExternalEvent.App.Commands;
using ricaun.Revit.UI.Tasks;

namespace RevitDevTool.ExternalEvent.App;

/// <summary>
///     Application entry point
/// </summary>
[UsedImplicitly]
public class Application : ExternalApplication
{
    public static RevitTaskService? RicaunService;
    public override void OnStartup()
    {
        RevitTask.Initialize(Application);
        RicaunService = new RevitTaskService(Application);
        RicaunService.Initialize();
        CreateRibbon();
    }

    private void CreateRibbon()
    {
        var panel = Application.CreatePanel("External Tools");

        panel.AddPushButton<ExternalEventCommand>("Benchmark\nExternalEvent")
            .SetImage("/RevitDevTool.ExternalEvent.App;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/RevitDevTool.ExternalEvent.App;component/Resources/Icons/RibbonIcon32.png");
        
        panel.AddPushButton<ContextLossCommand>("Context Loss")
            .SetImage("/RevitDevTool.ExternalEvent.App;component/Resources/Icons/RibbonIcon16.png")
            .SetLargeImage("/RevitDevTool.ExternalEvent.App;component/Resources/Icons/RibbonIcon32.png");
    }
}
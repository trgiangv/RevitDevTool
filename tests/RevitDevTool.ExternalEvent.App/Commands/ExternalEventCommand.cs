using RevitDevTool.ExternalEvent.App.Commands.Adapters;
using UIFramework;
namespace RevitDevTool.ExternalEvent.App.Commands;

[UsedImplicitly]
[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
internal class ExternalEventCommand : IExternalCommand, IExternalCommandAvailability
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var dispatchers = new List<IDispatchAdapter>
        {
            new RevitDevToolAdapter(),
            new RicaunTaskAdapter(Application.RicaunService!),
            new RevitAsyncAdapter(),
        };

        var inContextAdapters = new List<IInContextEventAdapter>
        {
            new NativeExternalEventAdapter(),
            new RevitToolkitAdapter(),
        };

        var window = new StressTestWindow(dispatchers, inContextAdapters)
        {
            Owner = MainWindow.getMainWnd()
        };
        window.Show();

        return Result.Succeeded;
    }

    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
    {
        return true;
    }
}

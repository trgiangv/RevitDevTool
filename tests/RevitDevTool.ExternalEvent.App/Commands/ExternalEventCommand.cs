using RevitDevTool.ExternalEvent.App.Commands.Adapters;
using UIFramework;
namespace RevitDevTool.ExternalEvent.App.Commands;

[UsedImplicitly]
[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
internal class ExternalEventCommand : IExternalCommand, IExternalCommandAvailability
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var adapters = new List<IDispatchAdapter>
        {
            new RevitDevToolAdapter(),
            new RevitToolkitAdapter(),
            new RevitAsyncAdapter(),
            new RicaunTaskAdapter(Application.RicaunService!)
        };
        
        var window = new StressTestWindow(adapters)
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

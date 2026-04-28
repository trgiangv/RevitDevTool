using Autodesk.Revit.Attributes;
using DevTools.Utilities;
using DevTools.Views.View;
using DevTools.Views.ViewModel;

namespace RevitDevTool.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class StubBuilderCommand : IExternalCommand, IExternalCommandAvailability
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var vm = new StubBuilderViewModel();
        var window = new StubBuilderWindow(vm);
        window.SetHostAppOwner();
        window.ShowDialog();
        return Result.Succeeded;
    }

    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
    {
        return true;
    }
}

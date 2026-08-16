using Autodesk.Revit.Attributes;
using DevTools.Presentation.ViewModels;
using DevTools.Presentation.Views;
using DevTools.UI;
using RevitDevTool.Core;

namespace RevitDevTool.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class StubBuilderCommand : IExternalCommand, IExternalCommandAvailability
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var vm = new StubBuilderViewModel(RevitContext.Application.VersionNumber);
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

using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using RevitDevTool.Utils;
using RevitDevTool.View;
using RevitDevTool.ViewModel;

namespace RevitDevTool.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class StubBuilderCommand : ExternalCommand, IExternalCommandAvailability
{
    public override void Execute()
    {
        var vm = new StubBuilderViewModel();
        var window = new StubBuilderWindow(vm);
        window.SetRevitOwner();
        window.ShowDialog();
    }

    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
    {
        return true;
    }
}

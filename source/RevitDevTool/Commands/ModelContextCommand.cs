#if REVIT2023_OR_GREATER
using Autodesk.Revit.Attributes;
using RevitDevTool.Tools.ModelsContext;

namespace RevitDevTool.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class ModelContextCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        Host.GetService<ModelsContextPane>().ToggleVisibility();
        return Result.Succeeded;
    }
}
#endif

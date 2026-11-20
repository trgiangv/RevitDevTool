using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

[Transaction(TransactionMode.Manual)]
public class Command : IExternalCommand
{
    private const string Value = "mc-live-step";
    private const bool Clear = false;
    private const int Edited = 3;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var document = commandData.Application.ActiveUIDocument.Document;
        var ducts = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_DuctCurves).WhereElementIsNotElementType()
            .Where(element => element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS) != null
                && !element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).IsReadOnly)
            .OrderBy(element => element.Id.Value)
            .Take(Edited)
            .ToList();
        if (ducts.Count < Edited)
        {
            message = "Only " + ducts.Count + " ducts take a comment in " + document.Title + ".";
            return Result.Failed;
        }

        var text = Clear ? string.Empty : Value;
        using (var transaction = new Transaction(document, Clear ? "Model Context step clear" : "Model Context step edit"))
        {
            transaction.Start();
            foreach (var duct in ducts)
            {
                duct.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(text);
            }

            transaction.Commit();
        }

        message = (Clear ? "Cleared Comments on " : "Set Comments = '" + Value + "' on ") + Edited + " ducts ("
            + string.Join(", ", ducts.Select(duct => duct.Id.ToString())) + ") in one undoable transaction.";
        return Result.Succeeded;
    }
}

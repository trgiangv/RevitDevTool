using Autodesk.Revit.UI;
using System.Diagnostics;
using Autodesk.Revit.Attributes;

namespace RevitDevTool.Revit.Command;

[Transaction(TransactionMode.Manual)]
public class ClearTraceGeometryCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;
        var hashKey = doc.GetHashCode();

        if (!TraceGeometryCommand.DocGeometries.TryGetValue(hashKey, out var value)) 
            return Result.Succeeded;
        
        var transaction = new Transaction(doc, "RemoveTransient");
        try
        {
            transaction.Start();
            doc.Delete(value);
            transaction.Commit();
        }
        catch (Exception e)
        {
            Trace.TraceWarning($"Remove Transient Geometry Failed : [{e.Message}]");
            transaction.RollBack();
        }
        finally
        {
            TraceGeometryCommand.DocGeometries.Remove(hashKey);
        }

        return Result.Succeeded;
    }
}
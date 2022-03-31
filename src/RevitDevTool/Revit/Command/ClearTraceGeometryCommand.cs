using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Diagnostics;

namespace RevitDevTool.Revit.Command
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ClearTraceGeometryCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            int hashKey = doc.GetHashCode();

            if (TraceGeometryCommand.DocGeometries.ContainsKey(hashKey))
            {
                Transaction transaction = new Transaction(doc, "RemoveTransient");
                foreach (var id in TraceGeometryCommand.DocGeometries[hashKey])
                {
                    try
                    {
                        transaction.Start();
                        doc.Delete(new ElementId(id));
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        Trace.TraceWarning($"Remove Transient Geometry Failed : [{id}]");
                        transaction.RollBack();
                        continue;
                    }
                }

            }

            return Result.Succeeded;
        }
    }
}

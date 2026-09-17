#r "C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/8.0.27/ref/net8.0/System.Runtime.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPI.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPIUI.dll"

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Collections.Generic;
using System.Diagnostics;

[Transaction(TransactionMode.Manual)]
public class LinkElementTraceCmd : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uidoc = commandData.Application.ActiveUIDocument;
        if (uidoc?.Document is null)
        {
            Trace.WriteLine("No active document. Open a model, then run again.");
            return Result.Cancelled;
        }

        IList<Reference> picks;
        try
        {
            picks = uidoc.Selection.PickObjects(ObjectType.LinkedElement, "Select linked element(s)");
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }

        var hostDoc = uidoc.Document;
        foreach (var pick in picks)
        {
            if (hostDoc.GetElement(pick.ElementId) is not RevitLinkInstance instance)
                continue;

            var linkDoc = instance.GetLinkDocument();
            var linked = linkDoc?.GetElement(pick.LinkedElementId);
            if (linked is null)
            {
                Trace.WriteLine($"Unloaded or missing link instance {instance.Id}");
                continue;
            }

            // Scoped tokens: {linkInstanceId}@{inner}. Click in the monitor to select + zoom.
            Trace.WriteLine($"Linked ElementId {instance.Id}@{linked.Id}");
            Trace.WriteLine($"Linked UniqueId {instance.Id}@{linked.UniqueId}");

            var ifc = linked.get_Parameter(BuiltInParameter.IFC_GUID)?.AsString();
            if (!string.IsNullOrEmpty(ifc))
                Trace.WriteLine($"Linked IfcGuid {instance.Id}@{ifc}");
        }

        return Result.Succeeded;
    }
}

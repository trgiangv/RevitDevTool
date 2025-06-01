using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Toolkit.External;

namespace RevitDevTool.Test;

[Transaction(TransactionMode.Manual)]
[UsedImplicitly]
public class CurveVisualization : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var curveRef = UiDocument.Selection.PickObject(ObjectType.Element,
                "Select Curve Element");
            var curve =
                Document.GetElement(curveRef)?.GetGeometryObjectFromReference(curveRef) as Curve;
            Trace.Write(curve);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Error in CurveVisualization: {e.Message}");
        }
    }
}

[Transaction(TransactionMode.Manual)]
[UsedImplicitly]
public class CurvesVisualization : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var curveRefs = UiDocument.Selection.PickObjects(ObjectType.Element,
                "Select Curve Elements");
            
            var curves = new List<Curve>();
            foreach (var curveRef in curveRefs)
            {
                var curve = Document.GetElement(curveRef)?.GetGeometryObjectFromReference(curveRef) as Curve;
                curves.Add(curve);
            }

            Trace.Write(curves);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Error in CurvesVisualization: {e.Message}");
        }

    }
}
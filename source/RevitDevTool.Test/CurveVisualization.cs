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
            var curveRef = UiDocument.Selection.PickObject(ObjectType.Edge, "Select Curve");
            var curve = Document.GetElement(curveRef)?.GetGeometryObjectFromReference(curveRef) as Edge;
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
            var curveRefs = UiDocument.Selection.PickObjects(ObjectType.Edge, "Select Curves");

            var curves = new List<Edge>();
            foreach (var curveRef in curveRefs)
            {
                var curve = Document.GetElement(curveRef)?.GetGeometryObjectFromReference(curveRef) as Edge;
                if (curve != null) curves.Add(curve);
            }

            Trace.Write(curves);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Error in CurvesVisualization: {e.Message}");
        }

    }
}
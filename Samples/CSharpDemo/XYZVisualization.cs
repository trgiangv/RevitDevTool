using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Toolkit.External;
using System.Diagnostics;

namespace CSharpDemo;

[Transaction(TransactionMode.Manual)]
[UsedImplicitly]
public class XyzVisualization : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var xyz = Application.ActiveUIDocument.Selection.PickObject(ObjectType.PointOnElement);
            var xyzPoint = xyz.GlobalPoint;
            Trace.Write(xyzPoint);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Error in XyzVisualization: {e.Message}");
        }
    }
}

[Transaction(TransactionMode.Manual)]
[UsedImplicitly]
public class XyzsVisualization : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var xyzRefs = Application.ActiveUIDocument.Selection.PickObjects(ObjectType.PointOnElement);
            var xyzs = xyzRefs.Select(x => x.GlobalPoint).ToList();
            if (xyzs.Count == 0)
            {
                Trace.TraceWarning("No points selected.");
                return;
            }
            Trace.Write(xyzs);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Error in XyzsVisualization: {e.Message}");
        }
    }
}
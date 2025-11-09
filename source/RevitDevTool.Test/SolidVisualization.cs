using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Toolkit.External;
using RevitDevTool.Test.Extensions;

namespace RevitDevTool.Test;

[Transaction(TransactionMode.Manual)]
[UsedImplicitly]
public class SolidVisualization : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var solidRef = UiDocument.Selection.PickObject(ObjectType.Element, "Select Solid Element");
            var solid = Document.GetElement(solidRef).GetSolids();
            if (solid.Count != 0)
            {
                Trace.Write(solid.First());
            }
            else
            {
                Trace.TraceWarning("No solid found for the selected element.");
            }
        }
        catch (Exception e)
        {
            Trace.TraceError($"Error in SolidVisualization: {e.Message}");
        }

    }
}

[Transaction(TransactionMode.Manual)]
[UsedImplicitly]
public class SolidsVisualization : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var solidRefs = UiDocument.Selection.PickObjects(ObjectType.Element, "Select Solid Elements");
            var solids = solidRefs.SelectMany(sRef => Document.GetElement(sRef).GetSolids()).ToList();
            if (solids.Count != 0)
            {
                Trace.Write(solids);
            }
            else
            {
                Trace.TraceWarning("No solids found for the selected elements.");
            }
        }
        catch (Exception e)
        {
            Trace.TraceError($"Error in SolidsVisualization: {e.Message}");
        }
    }
}
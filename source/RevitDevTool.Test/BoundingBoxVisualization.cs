using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Toolkit.External;

namespace RevitDevTool.Test;

[Transaction(TransactionMode.Manual)]
[UsedImplicitly]
public class BoundingBoxVisualization : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var elementRef = UiDocument.Selection.PickObject(ObjectType.Element, "Select Element");
            var element = Document.GetElement(elementRef);

            var bbox = element.get_BoundingBox(ActiveView);
            Trace.Write(bbox);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
           //
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error in BoundingBoxVisualization: {ex.Message}");
        }
    }
}

[Transaction(TransactionMode.Manual)]
[UsedImplicitly]
public class BoundingBoxesVisualization : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var elementRefs = UiDocument.Selection.PickObjects(ObjectType.Element, "Select Elements");
            var boxes = new List<BoundingBoxXYZ>();
            foreach (var elementRef in elementRefs)
            {
                var element = Document.GetElement(elementRef);
                var bbox = element.get_BoundingBox(ActiveView);
                if (bbox != null)
                {
                    boxes.Add(bbox);
                }
                else
                {
                    Trace.TraceWarning($"Element {element.Id} has no bounding box.");
                }
            }

            if (boxes.Count == 0)
            {
                Trace.TraceWarning("No bounding boxes found.");
                return;
            }

            Trace.Write(boxes);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error in BoundingBoxesVisualization: {ex.Message}");
        }
    }
}

[Transaction(TransactionMode.Manual)]
[UsedImplicitly]
public class OutlineVisualization : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var elementRef = UiDocument.Selection.PickObject(ObjectType.Element, "Select Element");
            var element = Document.GetElement(elementRef);

            var bbox = element.get_BoundingBox(ActiveView);
            var outline = new Outline(bbox.Min, bbox.Max);
            outline.Scale(2);
            Trace.Write(outline);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            //
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error in BoundingBoxVisualization: {ex.Message}");
        }
    }
}

[Transaction(TransactionMode.Manual)]
[UsedImplicitly]
public class OutlinesVisualization : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var elementRefs = UiDocument.Selection.PickObjects(ObjectType.Element, "Select Elements");
            var boxes = new List<Outline>();
            foreach (var elementRef in elementRefs)
            {
                var element = Document.GetElement(elementRef);
                var bbox = element.get_BoundingBox(ActiveView);
                if (bbox != null)
                {
                    var ol = new Outline(bbox.Min, bbox.Max);
                    ol.Scale(2);
                    boxes.Add(ol);
                }
                else
                {
                    Trace.TraceWarning($"Element {element.Id} has no bounding box.");
                }
            }

            if (boxes.Count == 0)
            {
                Trace.TraceWarning("No bounding boxes found.");
                return;
            }

            Trace.Write(boxes);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error in BoundingBoxesVisualization: {ex.Message}");
        }
    }
}
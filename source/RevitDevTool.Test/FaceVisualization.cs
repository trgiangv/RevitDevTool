using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Toolkit.External;
using System.Diagnostics;

namespace RevitDevTool.Test;

[Transaction(TransactionMode.Manual)]
[UsedImplicitly]
public class FaceVisualization : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var faceRef = UiDocument.Selection.PickObject(ObjectType.Face, "Select Face");
            var face = Document.GetElement(faceRef)?.GetGeometryObjectFromReference(faceRef) as Face;
            Trace.Write(face);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Error in FaceVisualization: {e.Message}");
        }
    }
}

[Transaction(TransactionMode.Manual)]
[UsedImplicitly]
public class FacesVisualization : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var faceRefs = UiDocument.Selection.PickObjects(ObjectType.Face, "Select Faces");
            var faces = new List<Face>();
            foreach (var faceRef in faceRefs)
            {
                var face = Document.GetElement(faceRef)?.GetGeometryObjectFromReference(faceRef) as Face;
                if (face != null)
                {
                    faces.Add(face);
                }
                else
                {
                    Trace.TraceWarning($"Face not found for reference: {faceRef}");
                }
            }

            Trace.Write(faces);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Error in FacesVisualization: {e.Message}");
        }
    }
}
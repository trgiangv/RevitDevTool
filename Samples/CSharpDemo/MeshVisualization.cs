using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Toolkit.External;
using CSharpDemo.Extensions;
using System.Diagnostics;

namespace CSharpDemo;

[Transaction(TransactionMode.Manual)]
[UsedImplicitly]
public class MeshVisualization : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var meshRef = Application.ActiveUIDocument.Selection.PickObject(ObjectType.Element, "Select Mesh Element");
            var mesh = Application.ActiveUIDocument.Document.GetElement(meshRef).GetMeshes();
            Trace.Write(mesh.First());
        }
        catch (Exception e)
        {
            Trace.TraceError($"Error in MeshVisualization: {e.Message}");
        }
    }
}

[Transaction(TransactionMode.Manual)]
[UsedImplicitly]
public class MeshesVisualization : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var meshRefs = Application.ActiveUIDocument.Selection.PickObjects(ObjectType.Element, "Select Mesh Elements");
            var meshes = meshRefs.SelectMany(mRef => Application.ActiveUIDocument.Document.GetElement(mRef).GetMeshes()).ToList();
            Trace.Write(meshes);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Error in MeshesVisualization: {e.Message}");
        }
    }
}
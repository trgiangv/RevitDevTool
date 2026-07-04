using System.Diagnostics;
using Python.Runtime;
using RevitDevTool.Controllers;
using RevitDevTool.Utils;
namespace RevitDevTool.Logging.Listeners;

public sealed class GeometryListener : TraceListener
{
    public override void Write(object? o)
    {
        var processed = ProcessGeometry(o);
        if (!processed)
            base.Write(o);
    }

    public override void Write(string? message) { }

    public override void WriteLine(string? message) { }

    private static bool ProcessGeometry(object? geometry) => geometry switch
    {
        // Single geometry object
        GeometryObject g => Trace(g),
        Plane plane => Trace(plane),
        XYZ xyz => Trace(xyz),
        BoundingBoxXYZ bbox => Trace(bbox),
        Outline outline => Trace(outline),

        // Revit array types
        FaceArray faceArray => Trace(faceArray.Cast<Face>()),
        CurveArray curveArray => Trace(curveArray.Cast<Curve>()),
        EdgeArray edgeArray => Trace(edgeArray.Cast<Edge>()),

        // Enumerable collections
        IEnumerable<CurveLoop> curveLoops => Trace(curveLoops.SelectMany(x => x)),
        IEnumerable<CurveArray> curveArrays => Trace(curveArrays.SelectMany(x => x.Cast<Curve>())),
        IEnumerable<EdgeArray> edgeArrays => Trace(edgeArrays.SelectMany(x => x.Cast<Edge>())),
        IEnumerable<FaceArray> faceArrays => Trace(faceArrays.SelectMany(x => x.Cast<Face>())),
        IEnumerable<GeometryObject> geometries => Trace(geometries),
        IEnumerable<Plane> planes => Trace(planes),
        IEnumerable<XYZ> xyz => Trace(xyz),
        IEnumerable<BoundingBoxXYZ> boxes => Trace(boxes),

        // Mixed object collection (typically IronPython/Cpython lists)
        ICollection<object> objects => ProcessMixedCollection(objects),
        PyObject pyObject => TryProcessPyObject(pyObject),

        _ => false
    };

    private static bool TryProcessPyObject(PyObject pyObject)
    {
        try
        {
            using (Py.GIL())
            {
                if (!pyObject.IsIterable() && pyObject is PyString) return false;
                var objects = pyObject.AsObjectCollection();
                return objects.Count != 0 && ProcessMixedCollection(objects);
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool Trace<T>(T geometry)
    {
        VisualizationController.Add(geometry);
        return true;
    }

    private static bool Trace<T>(IEnumerable<T> geometries)
    {
        VisualizationController.Add(geometries);
        return true;
    }

    private static bool ProcessMixedCollection(ICollection<object> objects)
    {
        var geometries = objects.SelectMany(ConvertToGeometryObjects).ToLookup(GetGeometryType);

        TraceGroupedGeometries(geometries);
        return true;
    }

    private static IEnumerable<object> ConvertToGeometryObjects(object geometry) => geometry switch
    {
        GeometryObject or Plane or BoundingBoxXYZ or Outline or XYZ => [geometry],
        CurveLoop curveLoop => curveLoop,
        CurveArray curveArray => curveArray.Cast<Curve>(),
        EdgeArray edgeArray => edgeArray.Cast<Edge>(),
        FaceArray faceArray => faceArray.Cast<Face>(),
        _ => []
    };

    private static Type GetGeometryType(object geometry) => geometry switch
    {
        GeometryObject => typeof(GeometryObject),
        Plane => typeof(Plane),
        BoundingBoxXYZ => typeof(BoundingBoxXYZ),
        Outline => typeof(Outline),
        XYZ => typeof(XYZ),
        _ => typeof(object)
    };

    private static void TraceGroupedGeometries(ILookup<Type, object> grouped)
    {
        foreach (var group in grouped)
        {
            if (group.Key == typeof(object)) continue;
            VisualizationController.Add(group.AsEnumerable());
        }
    }
}
using System.Diagnostics;
using System.Reflection;
using RevitDevTool.Handlers;

namespace RevitDevTool.Geometry;

public static class TraceGeometry
{
    public static readonly TraceListener TraceListener = new TraceGeometryListener();
    public static readonly Dictionary<int, List<ElementId>> DocGeometries = new();
    
    private static MethodInfo? GenerateTransientDisplayMethod()
    {
        var geometryElementType = typeof(GeometryElement);
        var geometryElementTypeMethods =
            geometryElementType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var method = geometryElementTypeMethods.FirstOrDefault(x => x.Name == "SetForTransientDisplay");
        return method;
    }

    private static void Trace(GeometryObject geometryObject)
    {
        Trace(new List<GeometryObject> { geometryObject });
    }

    private static void Trace(IEnumerable<GeometryObject> geometries)
    {
        ExternalEventController.ActionEventHandler.Raise(app =>
        {
            var method = GenerateTransientDisplayMethod();
            var doc = app.ActiveUIDocument.Document;
            var argsM = new object[4];
            argsM[0] = doc;
            argsM[1] = ElementId.InvalidElementId;
            argsM[2] = geometries;
            argsM[3] = ElementId.InvalidElementId;
            var transientElementId = method?.Invoke(null, argsM) as ElementId;
            
            var hashKey = doc.GetHashCode();
            if (DocGeometries.TryGetValue(hashKey, out var value))
            {
                if (transientElementId != null)
                    value.Add(transientElementId);
            }
            else
            {
                if (transientElementId != null)
                    DocGeometries[hashKey] = [transientElementId];
            }
        });
    }

    private class TraceGeometryListener : TraceListener
    {
        public override void Write(object? o)
        {
            if (o is GeometryObject go)
            {
                Trace(go);
            }
            if (o is IEnumerable<GeometryObject> geometries)
            {
                Trace(geometries);
            }

            base.Write(o);
        }
        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }
    }
}
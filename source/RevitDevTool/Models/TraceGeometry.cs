using System.Diagnostics;

namespace RevitDevTool.Models;

internal static class TraceGeometry
{
    public static readonly TraceListener TraceListener = new TraceGeometryListener();

    private static void Trace(object geometryObject)
    {
        VisualizationController.Add(geometryObject);
    }

    private static void Trace(IEnumerable<object> geometries)
    {
        VisualizationController.Add(geometries);
    }

    private class TraceGeometryListener : TraceListener
    {
        public override void Write(object? o)
        {
            switch (o)
            {
                case IEnumerable<GeometryObject> geometries:
                    Trace(geometries);
                    break;
                case GeometryObject geometryObject:
                    Trace(geometryObject);
                    break;
                case IEnumerable<CurveLoop> curves:
                    Trace(curves.SelectMany(x => x.ToList()));
                    break;
                case XYZ xyz:
                    Trace(xyz);
                    break;
                case BoundingBoxXYZ boundingBoxXyz:
                    Trace(boundingBoxXyz);
                    break;
                case IEnumerable<XYZ> xyzs:
                    Trace(xyzs);
                    break;
                case IEnumerable<BoundingBoxXYZ> boundingBoxXyzs:
                    Trace(boundingBoxXyzs);
                    break;
                default:
                    base.Write(o);
                    break;
            }
        }
        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
        }
    }
}
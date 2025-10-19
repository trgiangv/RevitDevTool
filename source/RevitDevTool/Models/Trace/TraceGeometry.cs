using System.Diagnostics ;

namespace RevitDevTool.Models.Trace;

internal static class TraceGeometry
{
    private static void Trace(object geometryObject)
    {
        VisualizationController.Add(geometryObject);
    }

    private static void Trace(IEnumerable<object> geometries)
    {
        VisualizationController.Add(geometries);
    }

    public class TraceGeometryListener : TraceListener
    {
        // ReSharper disable once CognitiveComplexity
        public override void Write(object? o)
        {
            switch (o)
            {
                case ICollection<object> geometries:
                    List<GeometryObject>? geometryObjects = null;
                    List<BoundingBoxXYZ>? boundingBoxes = null;
                    List<Outline>? outlines = null;
                    List<XYZ>? xyZs = null;

                    foreach (var geometry in geometries)
                    {
                        switch (geometry)
                        {
                            case GeometryObject geometryObject:
                                (geometryObjects ??= []).Add(geometryObject);
                                break;
                            case BoundingBoxXYZ boundingBox:
                                (boundingBoxes ??= []).Add(boundingBox);
                                break;
                            case Outline outline:
                                (outlines ??= []).Add(outline);
                                break;
                            case XYZ xyz:
                                (xyZs ??= []).Add(xyz);
                                break;
                            case CurveLoop curveLoop:
                                (geometryObjects ??= []).AddRange(curveLoop);
                                break;
                            case CurveArray curveArray:
                                (geometryObjects ??= []).AddRange(curveArray.Cast<Curve>());
                                break;
                            case EdgeArray edgeArray:
                                (geometryObjects ??= []).AddRange(edgeArray.Cast<Edge>());
                                break;
                            case FaceArray faceArray:
                                (geometryObjects ??= []).AddRange(faceArray.Cast<Face>());
                                break;
                            default:
                                base.Write(geometry);
                                break;
                        }
                    }
                    
                    if (geometryObjects?.Count > 0) Trace(geometryObjects);
                    if (boundingBoxes?.Count > 0) Trace(boundingBoxes);
                    if (outlines?.Count > 0) Trace(outlines);
                    if (xyZs?.Count > 0) Trace(xyZs);
                    break;
                case GeometryObject geometryObject:
                    Trace(geometryObject);
                    break;
                case XYZ xyz:
                    Trace(xyz);
                    break;
                case BoundingBoxXYZ boundingBoxXyz:
                    Trace(boundingBoxXyz);
                    break;
                case Outline outline:
                    Trace(outline);
                    break;
                case FaceArray faceArray:
                    Trace(faceArray.Cast<Face>());
                    break;
                case CurveArray curveArray:
                    Trace(curveArray.Cast<Curve>());
                    break;
                case EdgeArray edgeArray:
                    Trace(edgeArray.Cast<Edge>());
                    break;
                case IEnumerable<GeometryObject> geometries:
                    Trace(geometries);
                    break;
                case IEnumerable<CurveLoop> curves:
                    Trace(curves.SelectMany(x => x));
                    break;
                case IEnumerable<CurveArray> curveArrays:
                    Trace(curveArrays.SelectMany(x => x.Cast<Curve>()));
                    break;
                case IEnumerable<EdgeArray> edgeArrays:
                    Trace(edgeArrays.SelectMany(x => x.Cast<Edge>()));
                    break;
                case IEnumerable<FaceArray> faceArrays:
                    Trace(faceArrays.SelectMany(x => x.Cast<Face>()));
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
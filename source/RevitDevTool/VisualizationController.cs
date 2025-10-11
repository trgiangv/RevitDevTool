using RevitDevTool.Visualization.Server;

namespace RevitDevTool;

internal static class VisualizationController
{
    public static BoundingBoxVisualizationServer BoundingBoxVisualizationServer { get; } = new();
    public static MeshVisualizationServer MeshVisualizationServer { get; } = new();
    public static PolylineVisualizationServer PolylineVisualizationServer { get; } = new();
    public static SolidVisualizationServer SolidVisualizationServer { get; } = new();
    public static XyzVisualizationServer XyzVisualizationServer { get; } = new();
    public static FaceVisualizationServer FaceVisualizationServer { get; } = new();
    
    public static void Start()
    {
        BoundingBoxVisualizationServer.Register();
        MeshVisualizationServer.Register();
        PolylineVisualizationServer.Register();
        SolidVisualizationServer.Register();
        XyzVisualizationServer.Register();
        FaceVisualizationServer.Register();
    }
    
    public static void Stop()
    {
        BoundingBoxVisualizationServer.Unregister();
        MeshVisualizationServer.Unregister();
        PolylineVisualizationServer.Unregister();
        SolidVisualizationServer.Unregister();
        XyzVisualizationServer.Unregister();
        FaceVisualizationServer.Unregister();
    }
    
    public static void Clear()
    {
        BoundingBoxVisualizationServer.ClearGeometry();
        MeshVisualizationServer.ClearGeometry();
        PolylineVisualizationServer.ClearGeometry();
        SolidVisualizationServer.ClearGeometry();
        XyzVisualizationServer.ClearGeometry();
        FaceVisualizationServer.ClearGeometry();
    }
    
    public static void Add<T> (T geometry)
    {
        switch (geometry)
        {
            case BoundingBoxXYZ boundingBox:
                BoundingBoxVisualizationServer.AddGeometry(boundingBox);
                break;
            case Outline outline:
                var bbox = new BoundingBoxXYZ
                {
                    Min = outline.MinimumPoint,
                    Max = outline.MaximumPoint
                };
                BoundingBoxVisualizationServer.AddGeometry(bbox);
                break;
            case Mesh mesh:
                MeshVisualizationServer.AddGeometry(mesh);
                break;
            case Solid solid:
                SolidVisualizationServer.AddGeometry(solid);
                break;
            case XYZ xyz:
                XyzVisualizationServer.AddGeometry(xyz);
                break;
            case Curve curve:
                PolylineVisualizationServer.AddGeometry(curve);
                break;
            case Edge edge:
                PolylineVisualizationServer.AddGeometry(edge);
                break;
            case PolyLine polyline:
                PolylineVisualizationServer.AddGeometry(polyline);
                break;
            case Face face:
                FaceVisualizationServer.AddGeometry(face);
                break;
        }
    }
    
    public static void Add<T> (IEnumerable<T> geometries)
    {
        var groupedGeometries = geometries
            .GroupBy(geometry =>
            {
                return geometry switch
                {
                    BoundingBoxXYZ => typeof(BoundingBoxXYZ),
                    Outline => typeof(Outline),
                    Mesh => typeof(Mesh),
                    Solid => typeof(Solid),
                    XYZ => typeof(XYZ),
                    Face => typeof(Face),
                    Curve => typeof(Curve),
                    Edge => typeof(Edge),
                    PolyLine => typeof(PolyLine),
                    _ => null
                };
            });
        
        foreach (var group in groupedGeometries)
        {
            var geometryType = group.Key;
            if (geometryType is null) continue;

            switch (geometryType)
            {
                case not null when geometryType == typeof(BoundingBoxXYZ):
                    BoundingBoxVisualizationServer.AddGeometries(group.Cast<BoundingBoxXYZ>());
                    break;
                case not null when geometryType == typeof(Outline):
                    BoundingBoxVisualizationServer.AddGeometries(group.Cast<Outline>().Select(outline => 
                    new BoundingBoxXYZ {
                        Min = outline.MinimumPoint,
                        Max = outline.MaximumPoint
                    }));
                    break;
                case not null when geometryType == typeof(Mesh):
                    MeshVisualizationServer.AddGeometries(group.Cast<Mesh>());
                    break;
                case not null when geometryType == typeof(Solid):
                    SolidVisualizationServer.AddGeometries(group.Cast<Solid>());
                    break;
                case not null when geometryType == typeof(XYZ):
                    XyzVisualizationServer.AddGeometries(group.Cast<XYZ>());
                    break;
                case not null when geometryType == typeof(Face):
                    FaceVisualizationServer.AddGeometries(group.Cast<Face>());
                    break;
                case not null when geometryType == typeof(Curve):
                    PolylineVisualizationServer.AddGeometries(group.Cast<Curve>());
                    break;
                case not null when geometryType == typeof(Edge):
                    PolylineVisualizationServer.AddGeometries(group.Cast<Edge>());
                    break;
                case not null when geometryType == typeof(PolyLine):
                    PolylineVisualizationServer.AddGeometries(group.Cast<PolyLine>());
                    break;
            }
        }
    }
}
using RevitDevTool.ViewModel.Contracts ;
using RevitDevTool.ViewModel.Settings ;
using RevitDevTool.Visualization.Contracts ;
using RevitDevTool.Visualization.Server;

namespace RevitDevTool;

internal static class VisualizationController
{
    public static BoundingBoxVisualizationServerServer BoundingBoxVisualizationServerServer { get; } = new();
    public static MeshVisualizationServerServer MeshVisualizationServerServer { get; } = new();
    public static PolylineVisualizationServerServer PolylineVisualizationServerServer { get; } = new();
    public static SolidVisualizationServerServer SolidVisualizationServerServer { get; } = new();
    public static XyzVisualizationServerServer XyzVisualizationServerServer { get; } = new();
    public static FaceVisualizationServerServer FaceVisualizationServerServer { get; } = new();
    
    private static List<IVisualizationViewModel> VisualizationViewModels
    {
        get =>
        [
            BoundingBoxVisualizationViewModel.Instance,
            MeshVisualizationViewModel.Instance,
            PolylineVisualizationViewModel.Instance,
            SolidVisualizationViewModel.Instance,
            XyzVisualizationViewModel.Instance,
            FaceVisualizationViewModel.Instance
        ] ;
    }
    
    private static List<IVisualizationServerLifeCycle> VisualizationServers
    {
        get =>
        [
            BoundingBoxVisualizationServerServer,
            MeshVisualizationServerServer,
            PolylineVisualizationServerServer,
            SolidVisualizationServerServer,
            XyzVisualizationServerServer,
            FaceVisualizationServerServer
        ] ;
    }

    public static void Start()
    {
        foreach (var server in VisualizationServers)
        {
            server.Register();
        }

        foreach ( var viewModel in VisualizationViewModels ) 
        {
            viewModel.Initialize();
        }
    }
    
    public static void Stop()
    {
        foreach (var server in VisualizationServers)
        {
            server.Unregister();
        }
    }
    
    public static void Clear()
    {
        foreach (var server in VisualizationServers)
        {
            server.ClearGeometry();
        }
    }
    
    public static void Refresh()
    {
        foreach (var server in VisualizationViewModels)
        {
            server.Refresh();
        }
    }
    
    public static void Add<T> (T geometry)
    {
        switch (geometry)
        {
            case BoundingBoxXYZ boundingBox:
                BoundingBoxVisualizationServerServer.AddGeometry(boundingBox);
                break;
            case Outline outline:
                var bbox = new BoundingBoxXYZ
                {
                    Min = outline.MinimumPoint,
                    Max = outline.MaximumPoint
                };
                BoundingBoxVisualizationServerServer.AddGeometry(bbox);
                break;
            case Mesh mesh:
                MeshVisualizationServerServer.AddGeometry(mesh);
                break;
            case Solid solid:
                SolidVisualizationServerServer.AddGeometry(solid);
                break;
            case XYZ xyz:
                XyzVisualizationServerServer.AddGeometry(xyz);
                break;
            case Curve curve:
                PolylineVisualizationServerServer.AddGeometry(curve);
                break;
            case Edge edge:
                PolylineVisualizationServerServer.AddGeometry(edge);
                break;
            case PolyLine polyline:
                PolylineVisualizationServerServer.AddGeometry(polyline);
                break;
            case Face face:
                FaceVisualizationServerServer.AddGeometry(face);
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
                    BoundingBoxVisualizationServerServer.AddGeometries(group.Cast<BoundingBoxXYZ>());
                    break;
                case not null when geometryType == typeof(Outline):
                    BoundingBoxVisualizationServerServer.AddGeometries(group.Cast<Outline>().Select(outline => 
                    new BoundingBoxXYZ {
                        Min = outline.MinimumPoint,
                        Max = outline.MaximumPoint
                    }));
                    break;
                case not null when geometryType == typeof(Mesh):
                    MeshVisualizationServerServer.AddGeometries(group.Cast<Mesh>());
                    break;
                case not null when geometryType == typeof(Solid):
                    SolidVisualizationServerServer.AddGeometries(group.Cast<Solid>());
                    break;
                case not null when geometryType == typeof(XYZ):
                    XyzVisualizationServerServer.AddGeometries(group.Cast<XYZ>());
                    break;
                case not null when geometryType == typeof(Face):
                    FaceVisualizationServerServer.AddGeometries(group.Cast<Face>());
                    break;
                case not null when geometryType == typeof(Curve):
                    PolylineVisualizationServerServer.AddGeometries(group.Cast<Curve>());
                    break;
                case not null when geometryType == typeof(Edge):
                    PolylineVisualizationServerServer.AddGeometries(group.Cast<Edge>());
                    break;
                case not null when geometryType == typeof(PolyLine):
                    PolylineVisualizationServerServer.AddGeometries(group.Cast<PolyLine>());
                    break;
            }
        }
    }
}
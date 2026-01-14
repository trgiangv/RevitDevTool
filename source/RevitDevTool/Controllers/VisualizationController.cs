using RevitDevTool.ViewModel.Contracts;
using RevitDevTool.ViewModel.Settings.Visualization;
using RevitDevTool.Visualization.Contracts;
using RevitDevTool.Visualization.Server;
namespace RevitDevTool.Controllers;

internal static class VisualizationController
{
    private static BoundingBoxVisualizationServer? _boundingBoxVisualizationServer;
    private static MeshVisualizationServer? _meshVisualizationServer;
    private static PolylineVisualizationServer? _polylineVisualizationServer;
    private static SolidVisualizationServer? _solidVisualizationServer;
    private static XyzVisualizationServer? _xyzVisualizationServer;
    private static FaceVisualizationServer? _faceVisualizationServer;

    public static BoundingBoxVisualizationServer BoundingBoxVisualizationServer =>
        _boundingBoxVisualizationServer ??= Host.GetService<BoundingBoxVisualizationServer>();
    public static MeshVisualizationServer MeshVisualizationServer =>
        _meshVisualizationServer ??= Host.GetService<MeshVisualizationServer>();
    public static PolylineVisualizationServer PolylineVisualizationServer =>
        _polylineVisualizationServer ??= Host.GetService<PolylineVisualizationServer>();
    public static SolidVisualizationServer SolidVisualizationServer =>
        _solidVisualizationServer ??= Host.GetService<SolidVisualizationServer>();
    public static XyzVisualizationServer XyzVisualizationServer =>
        _xyzVisualizationServer ??= Host.GetService<XyzVisualizationServer>();
    public static FaceVisualizationServer FaceVisualizationServer =>
        _faceVisualizationServer ??= Host.GetService<FaceVisualizationServer>();

    private static List<(IVisualizationServerLifeCycle Server, IVisualizationViewModel ViewModel)>? _serverViewModelPairs;

    private static List<(IVisualizationServerLifeCycle Server, IVisualizationViewModel ViewModel)> ServerViewModelPairs =>
        _serverViewModelPairs ??=
        [
            (BoundingBoxVisualizationServer, Host.GetService<BoundingBoxVisualizationViewModel>()),
            (MeshVisualizationServer, Host.GetService<MeshVisualizationViewModel>()),
            (PolylineVisualizationServer, Host.GetService<PolylineVisualizationViewModel>()),
            (SolidVisualizationServer, Host.GetService<SolidVisualizationViewModel>()),
            (XyzVisualizationServer, Host.GetService<XyzVisualizationViewModel>()),
            (FaceVisualizationServer, Host.GetService<FaceVisualizationViewModel>())
        ];

    public static void Start()
    {
        foreach (var (server, viewModel) in ServerViewModelPairs)
        {
            server.Register(viewModel);
        }
    }

    public static void Stop()
    {
        foreach (var (server, _) in ServerViewModelPairs)
        {
            server.Unregister();
        }
    }

    public static void Clear()
    {
        foreach (var (server, _) in ServerViewModelPairs)
        {
            server.ClearGeometry();
        }
    }

    public static void Refresh()
    {
        foreach (var (_, viewModel) in ServerViewModelPairs)
        {
            viewModel.Refresh();
        }
        Context.ActiveUiDocument?.UpdateAllOpenViews();
    }

    public static void Add<T>(T? geometry)
    {
        switch (geometry)
        {
            case BoundingBoxXYZ boundingBox:
                BoundingBoxVisualizationServer.AddGeometry(boundingBox);
                break;
            case Outline outline:
                AddOutline(outline);
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

    public static void Add<T>(IEnumerable<T> geometries)
    {
        if (geometries is ICollection<T> collection)
        {
            switch (collection.Count)
            {
                case 0:
                    return;
                case 1:
                    Add(collection.First());
                    return;
            }
        }

        var grouped = geometries
            .GroupBy(GetGeometryType)
            .Where(g => g.Key is not null);

        foreach (var group in grouped)
        {
            AddGroupedGeometries(group.Key!, group);
        }
    }

    private static Type? GetGeometryType<T>(T geometry) => geometry switch
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

    private static void AddGroupedGeometries<T>(Type geometryType, IEnumerable<T> geometries)
    {
        if (geometryType == typeof(BoundingBoxXYZ))
            BoundingBoxVisualizationServer.AddGeometries(geometries.Cast<BoundingBoxXYZ>());
        else if (geometryType == typeof(Outline))
            AddOutlines(geometries.Cast<Outline>());
        else if (geometryType == typeof(Mesh))
            MeshVisualizationServer.AddGeometries(geometries.Cast<Mesh>());
        else if (geometryType == typeof(Solid))
            SolidVisualizationServer.AddGeometries(geometries.Cast<Solid>());
        else if (geometryType == typeof(XYZ))
            XyzVisualizationServer.AddGeometries(geometries.Cast<XYZ>());
        else if (geometryType == typeof(Face))
            FaceVisualizationServer.AddGeometries(geometries.Cast<Face>());
        else if (geometryType == typeof(Curve))
            PolylineVisualizationServer.AddGeometries(geometries.Cast<Curve>());
        else if (geometryType == typeof(Edge))
            PolylineVisualizationServer.AddGeometries(geometries.Cast<Edge>());
        else if (geometryType == typeof(PolyLine))
            PolylineVisualizationServer.AddGeometries(geometries.Cast<PolyLine>());
    }

    private static void AddOutline(Outline outline)
    {
        var bbox = new BoundingBoxXYZ
        {
            Min = outline.MinimumPoint,
            Max = outline.MaximumPoint
        };
        BoundingBoxVisualizationServer.AddGeometry(bbox);
    }

    private static void AddOutlines(IEnumerable<Outline> outlines)
    {
        var boxes = outlines.Select(outline => new BoundingBoxXYZ
        {
            Min = outline.MinimumPoint,
            Max = outline.MaximumPoint
        });
        BoundingBoxVisualizationServer.AddGeometries(boxes);
    }
}
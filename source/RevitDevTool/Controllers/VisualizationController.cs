using CommunityToolkit.Mvvm.Messaging;
using DevTools.Presentation.ViewModels.Messages;
using RevitDevTool.Core;
using RevitDevTool.ViewModel.Settings.Visualization;
using RevitDevTool.Visualization.Contracts;
using RevitDevTool.Visualization.Server;
namespace RevitDevTool.Controllers;

internal static class VisualizationController
{
    public static BoundingBoxVisualizationServer BoundingBoxVisualizationServer =>
        field ??= Host.GetService<BoundingBoxVisualizationServer>();
    public static MeshVisualizationServer MeshVisualizationServer =>
        field ??= Host.GetService<MeshVisualizationServer>();
    public static PolylineVisualizationServer PolylineVisualizationServer =>
        field ??= Host.GetService<PolylineVisualizationServer>();
    public static SolidVisualizationServer SolidVisualizationServer =>
        field ??= Host.GetService<SolidVisualizationServer>();
    public static XyzVisualizationServer XyzVisualizationServer =>
        field ??= Host.GetService<XyzVisualizationServer>();
    public static FaceVisualizationServer FaceVisualizationServer =>
        field ??= Host.GetService<FaceVisualizationServer>();
    public static PlaneVisualizationServer PlaneVisualizationServer =>
        field ??= Host.GetService<PlaneVisualizationServer>();

    private static IVisualizationServerLifeCycle[] Servers =>
        field ??=
        [
            BoundingBoxVisualizationServer,
            MeshVisualizationServer,
            PolylineVisualizationServer,
            SolidVisualizationServer,
            XyzVisualizationServer,
            FaceVisualizationServer,
            PlaneVisualizationServer
        ];

    private static IVisualizationViewModel[] ViewModels =>
        field ??=
        [
            Host.GetService<BoundingBoxVisualizationViewModel>(),
            Host.GetService<MeshVisualizationViewModel>(),
            Host.GetService<PolylineVisualizationViewModel>(),
            Host.GetService<SolidVisualizationViewModel>(),
            Host.GetService<XyzVisualizationViewModel>(),
            Host.GetService<FaceVisualizationViewModel>()
        ];

    public static void Start()
    {
        foreach (var server in Servers)
            server.Register();
        foreach (var viewModel in ViewModels)
            viewModel.Initialize();
    }

    public static void Stop()
    {
        foreach (var server in Servers)
            server.Unregister();
    }

    public static void Clear()
    {
        foreach (var server in Servers)
            server.ClearGeometry();
        NotifyGeometryCountChanged();
    }

    public static void NotifyGeometryCountChanged()
    {
        var totalGeometryCount = Servers.Sum(server => server.GeometryCount);
        Host.GetService<IMessenger>().Send(new GeometryCountChangedMessage(totalGeometryCount));
    }

    public static void Refresh()
    {
        foreach (var viewModel in ViewModels)
            viewModel.Refresh();
        RevitContext.ActiveUiDocument?.UpdateAllOpenViews();
    }

    public static void Add<T>(T? geometry)
    {
        switch (geometry)
        {
            case BoundingBoxXYZ boundingBox:
                BoundingBoxVisualizationServer.AddGeometry(boundingBox);
                return;
            case Outline outline:
                BoundingBoxVisualizationServer.AddGeometry(new BoundingBoxXYZ
                {
                    Min = outline.MinimumPoint,
                    Max = outline.MaximumPoint
                });
                return;
            case Mesh mesh:
                MeshVisualizationServer.AddGeometry(mesh);
                return;
            case Solid solid:
                SolidVisualizationServer.AddGeometry(solid);
                return;
            case XYZ xyz:
                XyzVisualizationServer.AddGeometry(xyz);
                return;
            case Curve curve:
                PolylineVisualizationServer.AddGeometry(curve);
                return;
            case Edge edge:
                PolylineVisualizationServer.AddGeometry(edge);
                return;
            case PolyLine polyline:
                PolylineVisualizationServer.AddGeometry(polyline);
                return;
            case Face face:
                FaceVisualizationServer.AddGeometry(face);
                return;
            case Plane plane:
                PlaneVisualizationServer.AddGeometry(plane);
                return;
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

        NotifyGeometryCountChanged();
    }

    private static Type? GetGeometryType<T>(T geometry) => geometry switch
    {
        BoundingBoxXYZ => typeof(BoundingBoxXYZ),
        Outline => typeof(Outline),
        Mesh => typeof(Mesh),
        Solid => typeof(Solid),
        XYZ => typeof(XYZ),
        Face => typeof(Face),
        Plane => typeof(Plane),
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
        else if (geometryType == typeof(Plane))
            PlaneVisualizationServer.AddGeometries(geometries.Cast<Plane>());
        else if (geometryType == typeof(Curve))
            PolylineVisualizationServer.AddGeometries(geometries.Cast<Curve>());
        else if (geometryType == typeof(Edge))
            PolylineVisualizationServer.AddGeometries(geometries.Cast<Edge>());
        else if (geometryType == typeof(PolyLine))
            PolylineVisualizationServer.AddGeometries(geometries.Cast<PolyLine>());
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

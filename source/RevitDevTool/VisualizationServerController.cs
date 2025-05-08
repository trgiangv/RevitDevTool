using RevitDevTool.Visualization.Server;

namespace RevitDevTool;

public class VisualizationServerController
{
    public static BoundingBoxVisualizationServer BoundingBoxVisualizationServer { get; private set; }
    public static MeshVisualizationServer MeshVisualizationServer { get; private set; }
    public static PolylineVisualizationServer PolylineVisualizationServer { get; private set; }
    public static SolidVisualizationServer SolidVisualizationServer { get; private set; }
    public static XyzVisualizationServer XyzVisualizationServer { get; private set; }
    public static FaceVisualizationServer FaceVisualizationServer { get; private set; }
    
    public static void Start()
    {
        var meshVisualizationServer = new MeshVisualizationServer();
        var polylineVisualizationServer = new PolylineVisualizationServer();
        var boudingBoxVisualizationServer = new BoundingBoxVisualizationServer();
        var solidVisualizationServer = new SolidVisualizationServer();
        var xyzVisualizationServer = new XyzVisualizationServer();
        var faceVisualizationServer = new FaceVisualizationServer();
        meshVisualizationServer.Register();
        polylineVisualizationServer.Register();
        boudingBoxVisualizationServer.Register();
        solidVisualizationServer.Register();
        xyzVisualizationServer.Register();
        faceVisualizationServer.Register();
    }
}
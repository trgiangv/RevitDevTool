using RevitDevTool.View.Settings ;

namespace RevitDevTool.ViewModel ;

public class SettingsViewModel
{
    public SolidVisualizationSettingsView SolidVisualizationSettingsView { get ; set ; }
    public XyzVisualizationSettingsView XyzVisualizationSettingsView { get ; set ; }
    public PolylineVisualizationSettingsView PolylineVisualizationSettingsView { get ; set ; }
    public FaceVisualizationSettingsView FaceVisualizationSettingsView { get ; set ; }
    public MeshVisualizationSettingsView MeshVisualizationSettingsView { get ; set ; }
    public BoundingBoxVisualizationSettingsView BoundingBoxVisualizationSettingsView { get ; set ; }
    public GeneralSettingsView GeneralSettingsView { get ; set ; }

    public SettingsViewModel()
    {
        SolidVisualizationSettingsView = new SolidVisualizationSettingsView() ;
        XyzVisualizationSettingsView = new XyzVisualizationSettingsView() ;
        FaceVisualizationSettingsView = new FaceVisualizationSettingsView() ;
        MeshVisualizationSettingsView = new MeshVisualizationSettingsView() ;
        PolylineVisualizationSettingsView = new PolylineVisualizationSettingsView() ;
        BoundingBoxVisualizationSettingsView = new BoundingBoxVisualizationSettingsView() ;
        GeneralSettingsView = new GeneralSettingsView() ;
    }
}
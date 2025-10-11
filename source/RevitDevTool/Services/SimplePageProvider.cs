using RevitDevTool.View.Settings ;
using Wpf.Ui.Abstractions;

namespace RevitDevTool.Services;

/// <summary>
/// A simple page provider that creates new instances of pages using reflection.
/// </summary>
public class SimplePageProvider : INavigationViewPageProvider
{
    private SolidVisualizationSettingsView? SolidVisualizationSettingsView { get ; set ; }
    private XyzVisualizationSettingsView? XyzVisualizationSettingsView { get ; set ; }
    private PolylineVisualizationSettingsView? PolylineVisualizationSettingsView { get ; set ; }
    private FaceVisualizationSettingsView? FaceVisualizationSettingsView { get ; set ; }
    private MeshVisualizationSettingsView? MeshVisualizationSettingsView { get ; set ; }
    private BoundingBoxVisualizationSettingsView? BoundingBoxVisualizationSettingsView { get ; set ; }
    private GeneralSettingsView? GeneralSettingsView { get ; set ; }
    
    /// <inheritdoc />
    public object? GetPage(Type? pageType)
    {
        if (pageType == null)
        {
            return null;
        }
        try 
        {
            return pageType switch
            {
                not null when pageType == typeof( SolidVisualizationSettingsView ) 
                    => SolidVisualizationSettingsView ??= (SolidVisualizationSettingsView) Activator.CreateInstance( pageType )!,
                not null when pageType == typeof( XyzVisualizationSettingsView ) 
                    => XyzVisualizationSettingsView ??= (XyzVisualizationSettingsView) Activator.CreateInstance( pageType )!,
                not null when pageType == typeof( PolylineVisualizationSettingsView )
                    => PolylineVisualizationSettingsView ??= (PolylineVisualizationSettingsView) Activator.CreateInstance( pageType )!,
                not null when pageType == typeof( FaceVisualizationSettingsView ) 
                    => FaceVisualizationSettingsView ??= (FaceVisualizationSettingsView) Activator.CreateInstance( pageType )!,
                not null when pageType == typeof( MeshVisualizationSettingsView ) 
                    => MeshVisualizationSettingsView ??= (MeshVisualizationSettingsView) Activator.CreateInstance( pageType )!,
                not null when pageType == typeof( BoundingBoxVisualizationSettingsView ) 
                    => BoundingBoxVisualizationSettingsView ??= (BoundingBoxVisualizationSettingsView) Activator.CreateInstance( pageType )!,
                not null when pageType == typeof( GeneralSettingsView ) 
                    => GeneralSettingsView ??= (GeneralSettingsView) Activator.CreateInstance( pageType )!,
                _ => pageType != null ? Activator.CreateInstance( pageType ) : null
            } ;
        }
        catch (Exception)
        {
            return null;
        }
    }
}



using System.Diagnostics;
using Autodesk.Revit.DB.DirectContext3D;
using Autodesk.Revit.DB.ExternalService;
using RevitDevTool.Visualization.Contracts ;
using Color = Autodesk.Revit.DB.Color ;

namespace RevitDevTool.Visualization.Server.Contracts;

public abstract class VisualizationServer<T> : IDirectContext3DServer, IVisualUpdate
{
    protected readonly List<T> VisualizeGeometries = [];
    protected bool HasGeometryUpdates = true;
    protected bool HasEffectsUpdates = true;
    protected readonly object RenderLock = new();
    
    public string GetVendorId() => "RevitDevTool";
    public bool CanExecute(Autodesk.Revit.DB.View dBView) => true;
    public string GetApplicationId() => string.Empty;
    public string GetSourceId() => string.Empty;
    public bool UsesHandles() => false;
    public ExternalServiceId GetServiceId() => ExternalServices.BuiltInExternalServices.DirectContext3DService;
    public string GetName() => $"{typeof(T).Name} Visualization Server";
    public string GetDescription() => $"Visualize and debug geometry of {typeof(T).Name}";
    
    public abstract Guid GetServerId();
    public abstract Outline? GetBoundingBox(Autodesk.Revit.DB.View dBView);
    public abstract bool UseInTransparentPass(Autodesk.Revit.DB.View dBView);
    public abstract void RenderScene(Autodesk.Revit.DB.View dBView, DisplayStyle displayStyle);
    
    public void ClearGeometry()
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;
        lock (RenderLock)
        {
            try
            {
                VisualizeGeometries.Clear();
                HasGeometryUpdates = true;
                DisposeBuffers();
                uiDocument.UpdateAllOpenViews();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error in {GetName()} ClearGeometry: {ex}");
            }
        }
    }
    
    public void AddGeometries(IEnumerable<T> geometries)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;
        lock (RenderLock)
        {
            try
            {
                VisualizeGeometries.AddRange(geometries);
                HasGeometryUpdates = true;
                HasEffectsUpdates = true;
                uiDocument.UpdateAllOpenViews();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error in {GetName()} AddGeometries: {ex}");
            }
        }
    }
    
    public void AddGeometry(T geometry)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;
        lock (RenderLock)
        {
            try
            {
                VisualizeGeometries.Add(geometry);
                HasGeometryUpdates = true;
                HasEffectsUpdates = true;
                uiDocument.UpdateAllOpenViews();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error in {GetName()} AddGeometry: {ex}");
            }
        }
    }

    public void Register()
    {
        ExternalEventController.ActionEventHandler.Raise(_ =>
        {
            var directContextService = (MultiServerService) 
                ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService);
            var serverIds = directContextService.GetActiveServerIds();
            if (directContextService.IsRegisteredServerId(GetServerId()))
            {
                Trace.TraceInformation("{0} already registered", GetName());
                return;
            }
            directContextService.AddServer(this);
            serverIds.Add(GetServerId());
            directContextService.SetActiveServers(serverIds);
            
            Trace.TraceInformation("{0} registered", GetName());
        });
    }
    
    public void Unregister()
    {
        ExternalEventController.ActionEventHandler.Raise(application =>
        {
            var directContextService = (MultiServerService) 
                ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService);
            if (!directContextService.IsRegisteredServerId(GetServerId()))
            {
                Trace.TraceInformation("{0} already unregistered", GetName());
                return;
            }
            
            directContextService.RemoveServer(GetServerId());
            
            Trace.TraceInformation("{0} unregistered", GetName());
            application.ActiveUIDocument?.UpdateAllOpenViews();
        });
    }

    protected virtual void DisposeBuffers()
    {
        
    }
    public virtual void UpdateEffects()
    {
        
    }
    public virtual void UpdateTransparency( double value )
    {
        
    }
    public virtual void UpdateSurfaceColor( Color color )
    {
        
    }
    public virtual void UpdateEdgeColor( Color color )
    {
        
    }
    public virtual void UpdateAxisColor( Color color )
    {
        
    }
    public virtual void UpdateCurveColor( Color color )
    {
        
    }
    public virtual void UpdateMeshGridColor( Color color )
    {
        
    }
    public virtual void UpdateNormalVectorColor( Color color )
    {
        
    }
    public virtual void UpdateDirectionColor( Color color )
    {
        
    }
    public virtual void UpdateXColor( Color color )
    {
        throw new NotImplementedException() ;
    }
    public virtual void UpdateYColor( Color color )
    {
        throw new NotImplementedException() ;
    }
    public virtual void UpdateZColor( Color color )
    {
        throw new NotImplementedException() ;
    }
    public virtual void UpdateScale( double value )
    {
        
    }
    public virtual void UpdateExtrusion( double value )
    {
        
    }
    public virtual void UpdateDiameter( double value )
    {
        
    }
    public virtual void UpdateAxisLength( double value )
    {
        
    }
    public virtual void UpdateSurfaceVisibility( bool visible )
    {
        
    }
    public virtual void UpdateEdgeVisibility( bool visible )
    {
        
    }
    public virtual void UpdateFaceVisibility( bool visible )
    {
        
    }
    public virtual void UpdateAxisVisibility( bool visible )
    {
        
    }
    public virtual void UpdateMeshGridVisibility( bool visible )
    {
        
    }
    public virtual void UpdateNormalVectorVisibility( bool visible )
    {
        
    }
    public virtual void UpdateCurveVisibility( bool visible )
    {
        
    }
    public virtual void UpdateDirectionVisibility( bool visible )
    {
        
    }
    public virtual void UpdatePlaneVisibility( bool visible )
    {
        
    }
    public virtual void UpdateXAxisVisibility( bool visible )
    {
        
    }
    public virtual void UpdateYAxisVisibility( bool visible )
    {
        
    }
    public virtual void UpdateZAxisVisibility( bool visible )
    {
        
    }
}
using System.Diagnostics ;
using Autodesk.Revit.DB.DirectContext3D ;
using Autodesk.Revit.DB.ExternalService ;
using RevitDevTool.ViewModel.Contracts ;
using Color = Autodesk.Revit.DB.Color ;

namespace RevitDevTool.Visualization.Contracts;

public abstract class VisualizationServer<TG> : IDirectContext3DServer, IVisualizationServerLifeCycle
{
    protected readonly List<TG> VisualizeGeometries = [];
    protected bool HasGeometryUpdates = true;
    protected bool HasEffectsUpdates = true;
    protected readonly object RenderLock = new();
    
    public string GetVendorId() => "RevitDevTool";
    public bool CanExecute(Autodesk.Revit.DB.View dBView) => true;
    public string GetApplicationId() => string.Empty;
    public string GetSourceId() => string.Empty;
    public bool UsesHandles() => false;
    public ExternalServiceId GetServiceId() => ExternalServices.BuiltInExternalServices.DirectContext3DService;
    public string GetName() => $"{typeof(TG).Name} Visualization Server";
    public string GetDescription() => $"Visualize and debug geometry of {typeof(TG).Name}";
    
    public abstract Guid GetServerId();
    public abstract Outline? GetBoundingBox(Autodesk.Revit.DB.View dBView);
    public abstract bool UseInTransparentPass(Autodesk.Revit.DB.View dBView);
    protected abstract void RenderScene();
    protected abstract void DisposeBuffers();

    public void RenderScene(Autodesk.Revit.DB.View dBView, DisplayStyle displayStyle)
    {
        lock (RenderLock) 
        {
            try 
            {
                RenderScene();
            }
            catch (Exception e) 
            {
                Trace.TraceError($"Error in {GetName()} RenderScene: {e}");
                ClearGeometry();
            }
        }
    }
    
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
    public void TriggerReRender()
    {
        HasEffectsUpdates = true;
        HasGeometryUpdates = true;
    }

    public void AddGeometries(IEnumerable<TG> geometries)
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
    
    public void AddGeometry(TG geometry)
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

    public void Register(IVisualizationViewModel visualizationViewModel)
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
            
            visualizationViewModel.Initialize();
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
}
using System.Diagnostics ;
using Autodesk.Revit.DB.DirectContext3D ;
using Autodesk.Revit.DB.ExternalService ;
using RevitDevTool.ViewModel.Contracts ;

namespace RevitDevTool.Visualization.Contracts;

public abstract class VisualizationServer<TG> : IDirectContext3DServer, IVisualizationServerLifeCycle
{
    protected readonly List<TG> visualizeGeometries = [];
    protected bool hasGeometryUpdates = true;
    protected bool hasEffectsUpdates = true;
    protected readonly object renderLock = new();
    
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

    protected static bool ShouldRenderTransparentPass(double transparency)
    {
        var isTransparentPass = DrawContext.IsTransparentPass();
        return isTransparentPass && transparency > 0 || !isTransparentPass && transparency == 0;
    }

    public void RenderScene(Autodesk.Revit.DB.View dBView, DisplayStyle displayStyle)
    {
        lock (renderLock) 
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
        lock (renderLock)
        {
            try
            {
                visualizeGeometries.Clear();
                hasGeometryUpdates = true;
                DisposeBuffers();
                uiDocument.UpdateAllOpenViews();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error in {GetName()} ClearGeometry: {ex}");
            }
        }
    }

    public void AddGeometries(IEnumerable<TG> geometries)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;
        lock (renderLock)
        {
            try
            {
                visualizeGeometries.AddRange(geometries);
                hasGeometryUpdates = true;
                hasEffectsUpdates = true;
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
        lock (renderLock)
        {
            try
            {
                visualizeGeometries.Add(geometry);
                hasGeometryUpdates = true;
                hasEffectsUpdates = true;
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
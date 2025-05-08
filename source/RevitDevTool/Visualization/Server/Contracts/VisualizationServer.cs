using System.Diagnostics;
using Autodesk.Revit.DB.DirectContext3D;
using Autodesk.Revit.DB.ExternalService;

namespace RevitDevTool.Visualization.Server.Contracts;

public abstract class VisualizationServer<T> : IDirectContext3DServer
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
            
            directContextService.AddServer(this);
            serverIds.Add(GetServerId());
            directContextService.SetActiveServers(serverIds);
            
            Trace.TraceInformation("{0} visualization server registered", GetName());
        });
    }
    
    public void Unregister()
    {
        ExternalEventController.ActionEventHandler.Raise(application =>
        {
            var directContextService = (MultiServerService) 
                ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService);
            directContextService.RemoveServer(GetServerId());
    
            application.ActiveUIDocument?.UpdateAllOpenViews();
        });
    }

    protected virtual void DisposeBuffers()
    {
        // Override in derived classes to dispose specific buffers
    }
}
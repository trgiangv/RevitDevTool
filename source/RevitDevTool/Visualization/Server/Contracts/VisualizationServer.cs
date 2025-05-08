using Autodesk.Revit.DB.DirectContext3D;
using Autodesk.Revit.DB.ExternalService;

namespace RevitDevTool.Visualization.Server.Contracts;

public abstract class VisualizationServer<T> : IDirectContext3DServer
{
    protected readonly List<T> VisualizeGeometries = [];
    protected bool HasGeometryUpdates = true;
    protected bool HasEffectsUpdates = true;
    
    protected readonly object RenderLock = new();
    private readonly Guid _guid = Guid.NewGuid();
    public Guid GetServerId() => _guid;
    public string GetVendorId() => "RevitDevTool";
    public bool CanExecute(Autodesk.Revit.DB.View dBView) => true;
    public string GetApplicationId() => string.Empty;
    public string GetSourceId() => string.Empty;
    public bool UsesHandles() => false;
    public ExternalServiceId GetServiceId() => ExternalServices.BuiltInExternalServices.DirectContext3DService;
    public string GetName() => $"{nameof(T)} visualization server";
    public string GetDescription() => $"Visualize and debug geometry of {nameof(T)}";
    
    public abstract Outline? GetBoundingBox(Autodesk.Revit.DB.View dBView);
    public abstract bool UseInTransparentPass(Autodesk.Revit.DB.View dBView);
    public abstract void RenderScene(Autodesk.Revit.DB.View dBView, DisplayStyle displayStyle);

    [UsedImplicitly]
    public void ClearGeometry()
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;
        lock (RenderLock)
        {
            VisualizeGeometries.Clear();
            HasGeometryUpdates = true;
            uiDocument.UpdateAllOpenViews();
        }
    }

    [UsedImplicitly]
    public void AddGeometries(IEnumerable<T> geometries)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;
        lock (RenderLock)
        {
            VisualizeGeometries.AddRange(geometries);
            HasEffectsUpdates = true;
            uiDocument.UpdateAllOpenViews();
        }
    }
    
    [UsedImplicitly]
    public void AddGeometry(T geometry)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;
        lock (RenderLock)
        {
            VisualizeGeometries.Add(geometry);
            HasEffectsUpdates = true;
            uiDocument.UpdateAllOpenViews();
        }
    }
    
    [UsedImplicitly]
    public void Register()
    {
        ExternalEventController.ActionEventHandler.Raise(application =>
        {
            if (application.ActiveUIDocument is null) return;

            var directContextService = (MultiServerService) 
                ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService);
            var serverIds = directContextService.GetActiveServerIds();

            directContextService.AddServer(this);
            serverIds.Add(GetServerId());
            directContextService.SetActiveServers(serverIds);

            application.ActiveUIDocument.UpdateAllOpenViews();
        });
    }

    [UsedImplicitly]
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
}
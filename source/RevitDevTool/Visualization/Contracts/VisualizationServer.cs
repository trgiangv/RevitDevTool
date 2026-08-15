using Autodesk.Revit.DB.DirectContext3D;
using Autodesk.Revit.DB.ExternalService;
using Microsoft.Extensions.Logging;
using RevitDevTool.Controllers;
using RevitDevTool.Core;
using ZLogger;

namespace RevitDevTool.Visualization.Contracts;

public abstract class VisualizationServer<Tg> : IDirectContext3DServer, IVisualizationServerLifeCycle
{
    protected ILogger? Logger { get; init; }
    protected readonly List<Tg> VisualizeGeometries = [];
    protected bool HasGeometryUpdates = true;
    protected bool HasEffectsUpdates = true;
    protected readonly object RenderLock = new();

    public int GeometryCount => VisualizeGeometries.Count;

    public string GetVendorId() => "RevitDevTool";
    public bool CanExecute(Autodesk.Revit.DB.View dBView) => true;
    public string GetApplicationId() => string.Empty;
    public string GetSourceId() => string.Empty;
    public bool UsesHandles() => false;
    public ExternalServiceId GetServiceId() => ExternalServices.BuiltInExternalServices.DirectContext3DService;
    public string GetName() => $"{typeof(Tg).Name} Visualization Server";
    public string GetDescription() => $"Visualize and debug geometry of {typeof(Tg).Name}";

    public abstract Guid GetServerId();
    public abstract Outline? GetBoundingBox(Autodesk.Revit.DB.View dBView);
    public abstract bool UseInTransparentPass(Autodesk.Revit.DB.View dBView);
    protected abstract void RenderScene();
    protected abstract void DisposeBuffers();

    protected static bool ShouldRenderTransparentPass(double transparency)
    {
        var isTransparentPass = DrawContext.IsTransparentPass();
        return (isTransparentPass && transparency > 0) || (!isTransparentPass && transparency == 0);
    }

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
                Logger?.ZLogError($"Error in {GetName()} RenderScene: {e}");
                ClearGeometry();
            }
        }
    }

    public void ClearGeometry()
    {
        var uiDocument = RevitContext.ActiveUiDocument;
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
                Logger?.ZLogError($"Error in {GetName()} ClearGeometry: {ex}");
            }
        }
    }

    internal void AddGeometries(IEnumerable<Tg> geometries)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
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
                Logger?.ZLogError($"Error in {GetName()} AddGeometries: {ex}");
            }
        }
    }

    public void AddGeometry(Tg geometry)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
        if (uiDocument is null) return;
        lock (RenderLock)
        {
            try
            {
                VisualizeGeometries.Add(geometry);
                HasGeometryUpdates = true;
                HasEffectsUpdates = true;
                uiDocument.UpdateAllOpenViews();
                VisualizationController.NotifyGeometryCountChanged();
            }
            catch (Exception ex)
            {
                Logger?.ZLogError($"Error in {GetName()} AddGeometry: {ex}");
            }
        }
    }

    public void Register()
    {
        RevitContextExecutor.Raise(() =>
        {
            var directContextService = (MultiServerService)
                ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService);
            var serverIds = directContextService.GetActiveServerIds();
            if (directContextService.IsRegisteredServerId(GetServerId()))
            {
#if DEBUG
                Logger?.ZLogDebug($"{GetName()} already registered");
#endif
                return;
            }
            directContextService.AddServer(this);
            serverIds.Add(GetServerId());
            directContextService.SetActiveServers(serverIds);

#if DEBUG
            Logger?.ZLogDebug($"{GetName()} registered");
#endif
        });
    }

    public void Unregister()
    {
        RevitContextExecutor.Raise(application =>
        {
            var directContextService = (MultiServerService)
                ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService);
            if (!directContextService.IsRegisteredServerId(GetServerId()))
            {
#if DEBUG
                Logger?.ZLogDebug($"{GetName()} already unregistered");
#endif
                return;
            }

            directContextService.RemoveServer(GetServerId());

#if DEBUG
            Logger?.ZLogDebug($"{GetName()} unregistered");
#endif
            application.ActiveUIDocument?.UpdateAllOpenViews();
        });
    }
}
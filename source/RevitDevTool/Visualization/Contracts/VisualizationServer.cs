using Autodesk.Revit.DB.DirectContext3D;
using Autodesk.Revit.DB.ExternalService;
using RevitDevTool.Controllers;
using System.Diagnostics;
using RevitDevTool.Core;
using RevitDevTool.ViewModel.Settings.Visualization;

namespace RevitDevTool.Visualization.Contracts;

public abstract class VisualizationServer<TG> : IDirectContext3DServer, IVisualizationServerLifeCycle
{
    protected readonly List<TG> VisualizeGeometries = [];
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
                Trace.TraceError($"Error in {GetName()} RenderScene: {e}");
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
                Trace.TraceError($"Error in {GetName()} ClearGeometry: {ex}");
            }
        }
    }

    public void AddGeometries(IEnumerable<TG> geometries)
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
                VisualizationController.NotifyGeometryCountChanged();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error in {GetName()} AddGeometries: {ex}");
            }
        }
    }

    public void AddGeometry(TG geometry)
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
                Trace.TraceError($"Error in {GetName()} AddGeometry: {ex}");
            }
        }
    }

    public void Register(IVisualizationViewModel visualizationViewModel)
    {
        RevitContextExecutor.Raise(() =>
        {
            var directContextService = (MultiServerService)
                ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService);
            var serverIds = directContextService.GetActiveServerIds();
            if (directContextService.IsRegisteredServerId(GetServerId()))
            {
                Debug.WriteLine($"{GetName()} already registered");
                return;
            }
            directContextService.AddServer(this);
            serverIds.Add(GetServerId());
            directContextService.SetActiveServers(serverIds);

            visualizationViewModel.Initialize();
            Debug.WriteLine($"{GetName()} registered");
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
                Debug.WriteLine($"{GetName()} already unregistered");
                return;
            }

            directContextService.RemoveServer(GetServerId());

            Debug.WriteLine($"{GetName()} unregistered");
            application.ActiveUIDocument?.UpdateAllOpenViews();
        });
    }
}
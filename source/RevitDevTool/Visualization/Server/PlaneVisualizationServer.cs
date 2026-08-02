using Autodesk.Revit.DB.DirectContext3D;
using DevTools.Utilities;
using Microsoft.Extensions.Logging;
using RevitDevTool.Core;
using RevitDevTool.Settings;
using RevitDevTool.Visualization.Contracts;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using ZLogger;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class PlaneVisualizationServer : VisualizationServer<Plane>
{
    private readonly Guid _serverId = new("8D179E0D-0F40-4C59-8D8B-ADAF8F41001A");
    public override Guid GetServerId() => _serverId;

    private readonly List<RenderingBufferStorage> _meshGridBuffers = [];
    private readonly List<RenderingBufferStorage> _normalBuffers = [];
    private readonly List<RenderingBufferStorage> _surfaceBuffers = [];

    private double _extrusion;
    private double _transparency;
    private Color _meshColor;
    private Color _normalColor;
    private Color _surfaceColor;
    private bool _drawMeshGrid;
    private bool _drawNormalVector;
    private bool _drawSurface;

    private const double BasePlaneHalfSize = 5;
    private const double PlaneScaleFactor = 50;
    private const double PlaneNormalLengthFactor = 0.5;
    private double PlaneHalfSize => BasePlaneHalfSize + _extrusion * PlaneScaleFactor;
    private double PlaneNormalLength => PlaneHalfSize * PlaneNormalLengthFactor;

    public PlaneVisualizationServer(IRevitSettingsService settingsService, ILogger<PlaneVisualizationServer> logger)
    {
        Logger = logger;
        var settings = settingsService.VisualizationConfig.FaceSettings;
        _extrusion = settings.Extrusion / 12.0;
        _transparency = settings.Transparency / 100.0;
        _meshColor = new Color(settings.MeshColor.R, settings.MeshColor.G, settings.MeshColor.B);
        _normalColor = new Color(settings.NormalVectorColor.R, settings.NormalVectorColor.G, settings.NormalVectorColor.B);
        _surfaceColor = new Color(settings.SurfaceColor.R, settings.SurfaceColor.G, settings.SurfaceColor.B);
        _drawMeshGrid = settings.ShowMeshGrid;
        _drawNormalVector = settings.ShowNormalVector;
        _drawSurface = settings.ShowSurface;
    }

    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawSurface && _transparency > 0;

    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view)
    {
        if (VisualizeGeometries.Count == 0) return null;

        var allPoints = new List<XYZ>();

        foreach (var plane in VisualizeGeometries)
        {
            var corners = RenderHelper.GetPlaneCorners(plane, PlaneHalfSize);
            allPoints.AddRange(corners);
            allPoints.Add(plane.Origin);
            allPoints.Add(plane.Origin + plane.Normal.Normalize() * PlaneNormalLength);
        }

        if (allPoints.Count == 0) return null;

        var min = new XYZ(
            allPoints.Min(p => p.X),
            allPoints.Min(p => p.Y),
            allPoints.Min(p => p.Z));

        var max = new XYZ(
            allPoints.Max(p => p.X),
            allPoints.Max(p => p.Y),
            allPoints.Max(p => p.Z));

        return new Outline(min, max);
    }

    protected override void RenderScene()
    {
        if (VisualizeGeometries.Count == 0) return;

        if (HasGeometryUpdates || _surfaceBuffers.Count == 0 || _meshGridBuffers.Count == 0 || _normalBuffers.Count == 0)
        {
            MapGeometryBuffer();
            HasGeometryUpdates = false;
        }

        if (HasEffectsUpdates)
        {
            UpdateEffects();
            HasEffectsUpdates = false;
        }

        RenderSurfaceBuffers();
        RenderMeshGridBuffers();
        RenderNormalBuffers();
    }

    private void RenderSurfaceBuffers()
    {
        if (!_drawSurface || _surfaceBuffers.Count == 0) return;
        if (!ShouldRenderTransparentPass(_transparency)) return;

        foreach (var surfaceBuffer in _surfaceBuffers)
        {
            DrawContext.FlushBuffer(
                surfaceBuffer.VertexBuffer,
                surfaceBuffer.VertexBufferCount,
                surfaceBuffer.IndexBuffer,
                surfaceBuffer.IndexBufferCount,
                surfaceBuffer.VertexFormat,
                surfaceBuffer.EffectInstance,
                PrimitiveType.TriangleList,
                0,
                surfaceBuffer.PrimitiveCount);
        }
    }

    private void RenderMeshGridBuffers()
    {
        if (!_drawMeshGrid || _meshGridBuffers.Count == 0) return;

        foreach (var meshGridBuffer in _meshGridBuffers)
        {
            DrawContext.FlushBuffer(
                meshGridBuffer.VertexBuffer,
                meshGridBuffer.VertexBufferCount,
                meshGridBuffer.IndexBuffer,
                meshGridBuffer.IndexBufferCount,
                meshGridBuffer.VertexFormat,
                meshGridBuffer.EffectInstance,
                PrimitiveType.LineList,
                0,
                meshGridBuffer.PrimitiveCount);
        }
    }

    private void RenderNormalBuffers()
    {
        if (!_drawNormalVector || _normalBuffers.Count == 0) return;

        foreach (var normalBuffer in _normalBuffers)
        {
            DrawContext.FlushBuffer(
                normalBuffer.VertexBuffer,
                normalBuffer.VertexBufferCount,
                normalBuffer.IndexBuffer,
                normalBuffer.IndexBufferCount,
                normalBuffer.VertexFormat,
                normalBuffer.EffectInstance,
                PrimitiveType.LineList,
                0,
                normalBuffer.PrimitiveCount);
        }
    }

    private void MapGeometryBuffer()
    {
        DisposeBuffers();

        if (VisualizeGeometries.Count == 0) return;

        try
        {
            foreach (var plane in VisualizeGeometries)
            {
                var surfaceBuffer = new RenderingBufferStorage();
                var meshGridBuffer = new RenderingBufferStorage();
                var normalBuffer = new RenderingBufferStorage();

                RenderHelper.MapPlaneBuffer(surfaceBuffer, plane, PlaneHalfSize);
                RenderHelper.MapPlaneGridBuffer(meshGridBuffer, plane, PlaneHalfSize);
                RenderHelper.MapNormalVectorBuffer(normalBuffer, plane.Origin, plane.Normal.Normalize(), PlaneNormalLength);

                _surfaceBuffers.Add(surfaceBuffer);
                _meshGridBuffers.Add(meshGridBuffer);
                _normalBuffers.Add(normalBuffer);
            }
        }
        catch (Exception ex)
        {
            Logger?.ZLogError($"Error mapping geometry buffer in PlaneVisualizationServer: {ex}");
        }
    }

    private void UpdateEffects()
    {
        foreach (var surfaceBuffer in _surfaceBuffers)
        {
            surfaceBuffer.EffectInstance ??= new EffectInstance(surfaceBuffer.FormatBits);
            surfaceBuffer.EffectInstance.SetColor(_surfaceColor);
            surfaceBuffer.EffectInstance.SetTransparency(_transparency);
        }

        foreach (var meshGridBuffer in _meshGridBuffers)
        {
            meshGridBuffer.EffectInstance ??= new EffectInstance(meshGridBuffer.FormatBits);
            meshGridBuffer.EffectInstance.SetColor(_meshColor);
        }

        foreach (var normalBuffer in _normalBuffers)
        {
            normalBuffer.EffectInstance ??= new EffectInstance(normalBuffer.FormatBits);
            normalBuffer.EffectInstance.SetColor(_normalColor);
        }
    }

    public void UpdateExtrusion(double value)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _extrusion = value;
            HasGeometryUpdates = true;
            HasEffectsUpdates = true;
            DisposeBuffers();
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateTransparency(double value)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _transparency = value;
            HasEffectsUpdates = true;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateSurfaceColor(Color value)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _surfaceColor = value;
            HasEffectsUpdates = true;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateMeshGridColor(Color value)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _meshColor = value;
            HasEffectsUpdates = true;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateNormalVectorColor(Color value)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _normalColor = value;
            HasEffectsUpdates = true;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateSurfaceVisibility(bool visible)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawSurface = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateMeshGridVisibility(bool visible)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawMeshGrid = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateNormalVectorVisibility(bool visible)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawNormalVector = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    protected override void DisposeBuffers()
    {
        _surfaceBuffers.Clear(true);
        _meshGridBuffers.Clear(true);
        _normalBuffers.Clear(true);
    }
}
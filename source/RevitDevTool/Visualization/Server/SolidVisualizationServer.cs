using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Settings;
using RevitDevTool.Utils;
using RevitDevTool.Visualization.Contracts;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using System.Diagnostics;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class SolidVisualizationServer : VisualizationServer<Solid>
{
    private readonly Guid _serverId = new("02B1803B-9008-427E-985F-4ED4DA839EF0");
    public override Guid GetServerId() => _serverId;
    private readonly List<RenderingBufferStorage> _faceBuffers = [];
    private readonly List<RenderingBufferStorage> _edgeBuffers = [];

    private double _transparency;
    private double _scale;
    private Color _faceColor;
    private Color _edgeColor;
    private bool _drawFace;
    private bool _drawEdge;

    public SolidVisualizationServer(ISettingsService settingsService)
    {
        var settings = settingsService.VisualizationConfig.SolidSettings;
        _transparency = settings.Transparency / 100;
        _scale = settings.Scale / 100;
        _faceColor = new Color(settings.FaceColor.R, settings.FaceColor.G, settings.FaceColor.B);
        _edgeColor = new Color(settings.EdgeColor.R, settings.EdgeColor.G, settings.EdgeColor.B);
        _drawFace = settings.ShowFace;
        _drawEdge = settings.ShowEdge;
    }

    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawFace && _transparency > 0;

    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view)
    {
        if (visualizeGeometries.Count == 0) return null;
        List<XYZ> minPoints = [];
        List<XYZ> maxPoints = [];

        foreach (var solid in visualizeGeometries)
        {
            if (solid.Volume == 0)
            {
                Debug.WriteLine("Solid with zero volume skipped in bounding box calculation.");
                continue;
            }
            BoundingBoxXYZ boundingBox;
            try { boundingBox = solid.GetBoundingBox(); }
            catch { continue; }

            var minPoint = boundingBox.Transform.OfPoint(boundingBox.Min);
            var maxPoint = boundingBox.Transform.OfPoint(boundingBox.Max);
            minPoints.Add(minPoint);
            maxPoints.Add(maxPoint);
        }

        var newMinPoint = minPoints
            .OrderBy(point => point.X)
            .ThenBy(point => point.Y)
            .ThenBy(point => point.Z)
            .FirstOrDefault();
        var newMaxPoint = maxPoints
            .OrderByDescending(point => point.X)
            .ThenByDescending(point => point.Y)
            .ThenByDescending(point => point.Z)
            .FirstOrDefault();
        return new Outline(newMinPoint, newMaxPoint);
    }

    protected override void RenderScene()
    {
        if (hasGeometryUpdates || _faceBuffers.Count == 0 || _edgeBuffers.Count == 0)
        {
            MapGeometryBuffer();
            hasGeometryUpdates = false;
        }

        if (hasEffectsUpdates)
        {
            UpdateEffects();
            hasEffectsUpdates = false;
        }

        RenderFaceBuffers();
        RenderEdgeBuffers();
    }

    private void RenderFaceBuffers()
    {
        if (!_drawFace || _faceBuffers.Count == 0) return;
        if (!ShouldRenderTransparentPass(_transparency)) return;

        foreach (var buffer in _faceBuffers)
        {
            DrawContext.FlushBuffer(buffer.VertexBuffer,
                buffer.VertexBufferCount,
                buffer.IndexBuffer,
                buffer.IndexBufferCount,
                buffer.VertexFormat,
                buffer.EffectInstance, PrimitiveType.TriangleList, 0,
                buffer.PrimitiveCount);
        }
    }

    private void RenderEdgeBuffers()
    {
        if (!_drawEdge || _edgeBuffers.Count == 0) return;

        foreach (var buffer in _edgeBuffers)
        {
            DrawContext.FlushBuffer(buffer.VertexBuffer,
                buffer.VertexBufferCount,
                buffer.IndexBuffer,
                buffer.IndexBufferCount,
                buffer.VertexFormat,
                buffer.EffectInstance, PrimitiveType.LineList, 0,
                buffer.PrimitiveCount);
        }
    }

    private void MapGeometryBuffer()
    {
        DisposeBuffers();

        foreach (var solid in visualizeGeometries)
        {
            if (solid.Volume == 0) continue;
            var scaledSolid = RenderGeometryHelper.ScaleSolid(solid, _scale);

            foreach (Face face in scaledSolid.Faces)
            {
                var buffer = new RenderingBufferStorage();
                MapFaceBuffers(buffer, face);
                _faceBuffers.Add(buffer);
            }

            foreach (Edge edge in scaledSolid.Edges)
            {
                var buffer = new RenderingBufferStorage();
                MapEdgeBuffers(buffer, edge);
                _edgeBuffers.Add(buffer);
            }
        }
    }

    private static void MapFaceBuffers(RenderingBufferStorage buffer, Face face)
    {
        var mesh = face.Triangulate();
        RenderHelper.MapSurfaceBuffer(buffer, mesh, 0);
    }

    private static void MapEdgeBuffers(RenderingBufferStorage buffer, Edge edge)
    {
        var mesh = edge.Tessellate();
        RenderHelper.MapCurveBuffer(buffer, mesh);
    }

    private void UpdateEffects()
    {
        foreach (var buffer in _faceBuffers)
        {
            buffer.EffectInstance ??= new EffectInstance(buffer.FormatBits);
            buffer.EffectInstance.SetColor(_faceColor);
            buffer.EffectInstance.SetTransparency(_transparency);
        }

        foreach (var buffer in _edgeBuffers)
        {
            buffer.EffectInstance ??= new EffectInstance(buffer.FormatBits);
            buffer.EffectInstance.SetColor(_edgeColor);
        }
    }

    public void UpdateSurfaceColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _faceColor = value;
            hasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateEdgeColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _edgeColor = value;
            hasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateTransparency(double value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _transparency = value;
            hasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateScale(double value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        _scale = value;

        lock (renderLock)
        {
            hasGeometryUpdates = true;
            hasEffectsUpdates = true;
            DisposeBuffers();

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateFaceVisibility(bool value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _drawFace = value;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateEdgeVisibility(bool value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _drawEdge = value;

            uiDocument.UpdateAllOpenViews();
        }
    }

    protected override void DisposeBuffers()
    {
        _faceBuffers.Clear(true);
        _edgeBuffers.Clear(true);
    }
}
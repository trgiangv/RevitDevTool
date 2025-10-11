using System.Diagnostics;
using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Extensions ;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using RevitDevTool.Visualization.Server.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class FaceVisualizationServer : VisualizationServer<Face>
{
    private readonly Guid _serverId = new("F67DFF33-B5A8-47AA-AB81-557A032C001E");
    public override Guid GetServerId() => _serverId;
    private readonly List<RenderingBufferStorage> _meshGridBuffers = [];
    private readonly List<RenderingBufferStorage> _normalBuffers = [];
    private readonly List<RenderingBufferStorage> _surfaceBuffers = [];

    private double _extrusion;
    private double _transparency;

    private Color _meshColor ;
    private Color _normalColor ;
    private Color _surfaceColor ;

    private bool _drawMeshGrid ;
    private bool _drawNormalVector ;
    private bool _drawSurface ;
    
    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawSurface && _transparency > 0;

    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view)
    {
        if (VisualizeGeometries.Count == 0) return null;
        List<XYZ> minPoints = [];
        List<XYZ> maxPoints = [];
        foreach (var face in VisualizeGeometries)
        {
            if (face.Reference is null) return null;

            var element = face.Reference.ElementId.ToElement(view.Document)!;
            var boundingBox = element.get_BoundingBox(null) ?? element.get_BoundingBox(view);
            if (boundingBox is null) return null;

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

    public override void RenderScene(Autodesk.Revit.DB.View view, DisplayStyle displayStyle)
    {
        lock (RenderLock)
        {
            if (VisualizeGeometries.Count == 0) return;

            try
            {
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

                if (_drawSurface && _surfaceBuffers.Count != 0)
                {
                    var isTransparentPass = DrawContext.IsTransparentPass();
                    if ((isTransparentPass && _transparency > 0) || (!isTransparentPass && _transparency == 0))
                    {
                        foreach (var surfaceBuffer in _surfaceBuffers)
                        {
                            DrawContext.FlushBuffer(surfaceBuffer.VertexBuffer,
                                surfaceBuffer.VertexBufferCount,
                                surfaceBuffer.IndexBuffer,
                                surfaceBuffer.IndexBufferCount,
                                surfaceBuffer.VertexFormat,
                                surfaceBuffer.EffectInstance, PrimitiveType.TriangleList, 0,
                                surfaceBuffer.PrimitiveCount);
                        }
                    }
                }

                if (_drawMeshGrid && _meshGridBuffers.Count != 0)
                {
                    foreach (var meshGridBuffer in _meshGridBuffers)
                    {
                        DrawContext.FlushBuffer(meshGridBuffer.VertexBuffer,
                            meshGridBuffer.VertexBufferCount,
                            meshGridBuffer.IndexBuffer,
                            meshGridBuffer.IndexBufferCount,
                            meshGridBuffer.VertexFormat,
                            meshGridBuffer.EffectInstance, PrimitiveType.LineList, 0,
                            meshGridBuffer.PrimitiveCount);
                    }
                }

                if (_drawNormalVector && _normalBuffers.Count != 0)
                {
                    foreach (var normalBuffer in _normalBuffers)
                    {
                        DrawContext.FlushBuffer(normalBuffer.VertexBuffer,
                            normalBuffer.VertexBufferCount,
                            normalBuffer.IndexBuffer,
                            normalBuffer.IndexBufferCount,
                            normalBuffer.VertexFormat,
                            normalBuffer.EffectInstance, PrimitiveType.LineList, 0,
                            normalBuffer.PrimitiveCount);
                    }
                }
            }
            catch (Exception exception)
            {
                Trace.TraceError($"Error in FaceVisualizationServer: {exception.Message}");
            }
        }
    }

    private void MapGeometryBuffer()
    {
        DisposeBuffers();

        if (VisualizeGeometries.Count == 0) return;
        
        try
        {
            foreach (var face in VisualizeGeometries)
            {
                var mesh = face.Triangulate();
                var faceBox = face.GetBoundingBox();
                var center = (faceBox.Min + faceBox.Max) / 2;
                var normal = face.ComputeNormal(center);
                var offset = RenderGeometryHelper.InterpolateOffsetByArea(face.Area);
                var normalLength = RenderGeometryHelper.InterpolateAxisLengthByArea(face.Area);

                var surfaceBuffer = new RenderingBufferStorage();
                var meshGridBuffer = new RenderingBufferStorage();
                var normalBuffer = new RenderingBufferStorage();
                RenderHelper.MapSurfaceBuffer(surfaceBuffer, mesh, _extrusion);
                RenderHelper.MapMeshGridBuffer(meshGridBuffer, mesh, _extrusion);
                RenderHelper.MapNormalVectorBuffer(normalBuffer, face.Evaluate(center) + normal * (offset + _extrusion), normal, normalLength);

                _surfaceBuffers.Add(surfaceBuffer);
                _meshGridBuffers.Add(meshGridBuffer);
                _normalBuffers.Add(normalBuffer);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error mapping geometry buffer in FaceVisualizationServer: {ex}");
        }
    }

    public override void UpdateEffects()
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
    
    public override void UpdateSurfaceColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _surfaceColor = value;
            HasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public override void UpdateMeshGridColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _meshColor = value;
            HasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public override void UpdateNormalVectorColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _normalColor = value;
            HasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public override void UpdateExtrusion(double value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _extrusion = value;
            HasGeometryUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public override void UpdateTransparency(double value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _transparency = value;
            HasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public override void UpdateSurfaceVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawSurface = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public override void UpdateMeshGridVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawMeshGrid = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public override void UpdateNormalVectorVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
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
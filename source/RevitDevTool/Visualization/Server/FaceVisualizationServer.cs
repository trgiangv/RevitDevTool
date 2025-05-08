using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using RevitDevTool.Visualization.Server.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class FaceVisualizationServer : VisualizationServer<Face>
{
    private readonly RenderingBufferStorage _meshGridBuffer = new();
    private readonly RenderingBufferStorage _normalBuffer = new();
    private readonly RenderingBufferStorage _surfaceBuffer = new();

    private double _extrusion = FaceVisualizationSettings.Extrusion;
    private double _transparency = FaceVisualizationSettings.Transparency;

    private Color _meshColor = new(
        FaceVisualizationSettings.MeshColor.R,
        FaceVisualizationSettings.MeshColor.G,
        FaceVisualizationSettings.MeshColor.B);
    private Color _normalColor = new(
        FaceVisualizationSettings.NormalVectorColor.R, 
        FaceVisualizationSettings.NormalVectorColor.G, 
        FaceVisualizationSettings.NormalVectorColor.B);
    private Color _surfaceColor = new(
        FaceVisualizationSettings.SurfaceColor.R, 
        FaceVisualizationSettings.SurfaceColor.G, 
        FaceVisualizationSettings.SurfaceColor.B);

    private bool _drawMeshGrid = FaceVisualizationSettings.ShowMeshGrid;
    private bool _drawNormalVector = FaceVisualizationSettings.ShowNormalVector;
    private bool _drawSurface = FaceVisualizationSettings.ShowSurface;
    
    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawSurface && _transparency > 0;

    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view)
    {
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
            try
            {
                if (HasGeometryUpdates || !_surfaceBuffer.IsValid() || !_meshGridBuffer.IsValid() || !_normalBuffer.IsValid())
                {
                    MapGeometryBuffer();
                    HasGeometryUpdates = false;
                }

                if (HasEffectsUpdates)
                {
                    UpdateEffects();
                    HasEffectsUpdates = false;
                }

                if (_drawSurface)
                {
                    var isTransparentPass = DrawContext.IsTransparentPass();
                    if (isTransparentPass && _transparency > 0 || !isTransparentPass && _transparency == 0)
                    {
                        DrawContext.FlushBuffer(_surfaceBuffer.VertexBuffer,
                            _surfaceBuffer.VertexBufferCount,
                            _surfaceBuffer.IndexBuffer,
                            _surfaceBuffer.IndexBufferCount,
                            _surfaceBuffer.VertexFormat,
                            _surfaceBuffer.EffectInstance, PrimitiveType.TriangleList, 0,
                            _surfaceBuffer.PrimitiveCount);
                    }
                }

                if (_drawMeshGrid)
                {
                    DrawContext.FlushBuffer(_meshGridBuffer.VertexBuffer,
                        _meshGridBuffer.VertexBufferCount,
                        _meshGridBuffer.IndexBuffer,
                        _meshGridBuffer.IndexBufferCount,
                        _meshGridBuffer.VertexFormat,
                        _meshGridBuffer.EffectInstance, PrimitiveType.LineList, 0,
                        _meshGridBuffer.PrimitiveCount);
                }

                if (_drawNormalVector)
                {
                    DrawContext.FlushBuffer(_normalBuffer.VertexBuffer,
                        _normalBuffer.VertexBufferCount,
                        _normalBuffer.IndexBuffer,
                        _normalBuffer.IndexBufferCount,
                        _normalBuffer.VertexFormat,
                        _normalBuffer.EffectInstance, PrimitiveType.LineList, 0,
                        _normalBuffer.PrimitiveCount);
                }
            }
            catch (Exception exception)
            {
                RenderFailed?.Invoke(this, new RenderFailedEventArgs
                {
                    Exception = exception
                });
            }
        }
    }

    private void MapGeometryBuffer()
    {
        foreach (var face in VisualizeGeometries)
        {
            var mesh = face.Triangulate();
            var faceBox = face.GetBoundingBox();
            var center = (faceBox.Min + faceBox.Max) / 2;
            var normal = face.ComputeNormal(center);
            var offset = RenderGeometryHelper.InterpolateOffsetByArea(face.Area);
            var normalLength = RenderGeometryHelper.InterpolateAxisLengthByArea(face.Area);

            RenderHelper.MapSurfaceBuffer(_surfaceBuffer, mesh, _extrusion);
            RenderHelper.MapMeshGridBuffer(_meshGridBuffer, mesh, _extrusion);
            RenderHelper.MapNormalVectorBuffer(_normalBuffer, face.Evaluate(center) + normal * (offset + _extrusion), normal, normalLength);
        }
    }

    private void UpdateEffects()
    {
        _surfaceBuffer.EffectInstance ??= new EffectInstance(_surfaceBuffer.FormatBits);
        _meshGridBuffer.EffectInstance ??= new EffectInstance(_surfaceBuffer.FormatBits);
        _normalBuffer.EffectInstance ??= new EffectInstance(_surfaceBuffer.FormatBits);

        _surfaceBuffer.EffectInstance.SetColor(_surfaceColor);
        _meshGridBuffer.EffectInstance.SetColor(_meshColor);
        _normalBuffer.EffectInstance.SetColor(_normalColor);
        _surfaceBuffer.EffectInstance.SetTransparency(_transparency);
    }

    public void UpdateSurfaceColor(Color value)
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

    public void UpdateMeshGridColor(Color value)
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

    public void UpdateNormalVectorColor(Color value)
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

    public void UpdateExtrusion(double value)
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

    public void UpdateTransparency(double value)
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

    public void UpdateSurfaceVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawSurface = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateMeshGridVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawMeshGrid = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateNormalVectorVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawNormalVector = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public event EventHandler<RenderFailedEventArgs>? RenderFailed;
}
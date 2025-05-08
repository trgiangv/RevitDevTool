using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using RevitDevTool.Visualization.Server.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class MeshVisualizationServer : VisualizationServer<Mesh>
{
    private readonly RenderingBufferStorage[] _normalBuffers = [];
    private readonly RenderingBufferStorage _surfaceBuffer = new();
    private readonly RenderingBufferStorage _meshGridBuffer = new();

    private double _extrusion;
    private double _transparency;

    private bool _drawMeshGrid;
    private bool _drawNormalVector;
    private bool _drawSurface;

    private Color _meshColor = new(
        MeshVisualizationSettings.MeshColor.R,
        MeshVisualizationSettings.MeshColor.G, 
        MeshVisualizationSettings.MeshColor.B);
    private Color _normalColor = new(
        MeshVisualizationSettings.NormalVectorColor.R, 
        MeshVisualizationSettings.NormalVectorColor.G, 
        MeshVisualizationSettings.NormalVectorColor.B);
    private Color _surfaceColor = new(
        MeshVisualizationSettings.SurfaceColor.R, 
        MeshVisualizationSettings.SurfaceColor.G, 
        MeshVisualizationSettings.SurfaceColor.B);
    
    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawSurface && _transparency > 0;
    
    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view)
    {
        return null;
    }

    public override void RenderScene(Autodesk.Revit.DB.View view, DisplayStyle displayStyle)
    {
        lock (RenderLock)
        {
            try
            {
                if (HasGeometryUpdates || !_surfaceBuffer.IsValid() || !_meshGridBuffer.IsValid())
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
                    foreach (var buffer in _normalBuffers)
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
        foreach (var mesh in VisualizeGeometries)
        {
            RenderHelper.MapSurfaceBuffer(_surfaceBuffer, mesh, _extrusion);
            RenderHelper.MapMeshGridBuffer(_meshGridBuffer, mesh, _extrusion);
            MapNormalsBuffer();
        }
    }

    private void MapNormalsBuffer()
    {
        foreach (var mesh in VisualizeGeometries)
        {
            var area = RenderGeometryHelper.ComputeMeshSurfaceArea(mesh);
            var offset = RenderGeometryHelper.InterpolateOffsetByArea(area);
            var normalLength = RenderGeometryHelper.InterpolateAxisLengthByArea(area);

            for (var i = 0; i < mesh.Vertices.Count; i++)
            {
                var vertex = mesh.Vertices[i];
                var buffer = _normalBuffers[i];
                var normal = RenderGeometryHelper.GetMeshVertexNormal(mesh, i, mesh.DistributionOfNormals);

                RenderHelper.MapNormalVectorBuffer(buffer, vertex + normal * (offset + _extrusion), normal, normalLength);
            }
        }
    }

    private void UpdateEffects()
    {
        _surfaceBuffer.EffectInstance ??= new EffectInstance(_surfaceBuffer.FormatBits);
        _meshGridBuffer.EffectInstance ??= new EffectInstance(_surfaceBuffer.FormatBits);

        _surfaceBuffer.EffectInstance.SetColor(_surfaceColor);
        _meshGridBuffer.EffectInstance.SetColor(_meshColor);
        _surfaceBuffer.EffectInstance.SetTransparency(_transparency);

        foreach (var normalBuffer in _normalBuffers)
        {
            normalBuffer.EffectInstance ??= new EffectInstance(_surfaceBuffer.FormatBits);
            normalBuffer.EffectInstance.SetColor(_normalColor);
        }
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
using System.Diagnostics;
using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using RevitDevTool.Visualization.Server.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class MeshVisualizationServer : VisualizationServer<Mesh>
{
    private readonly Guid _serverId = new("FD6F0E82-26D9-485B-A6D8-5FA65B85442F");
    public override Guid GetServerId() => _serverId;
    private RenderingBufferStorage[] _normalBuffers = [];
    private readonly RenderingBufferStorage _surfaceBuffer = new();
    private readonly RenderingBufferStorage _meshGridBuffer = new();

    private readonly double _extrusion = MeshVisualizationSettings.Extrusion;
    private readonly double _transparency = MeshVisualizationSettings.Transparency;

    private readonly bool _drawMeshGrid = MeshVisualizationSettings.ShowMeshGrid;
    private readonly bool _drawNormalVector = MeshVisualizationSettings.ShowNormalVector;
    private readonly bool _drawSurface = MeshVisualizationSettings.ShowSurface;

    private readonly Color _meshColor = new(
        MeshVisualizationSettings.MeshColor.R,
        MeshVisualizationSettings.MeshColor.G, 
        MeshVisualizationSettings.MeshColor.B);
    private readonly Color _normalColor = new(
        MeshVisualizationSettings.NormalVectorColor.R, 
        MeshVisualizationSettings.NormalVectorColor.G, 
        MeshVisualizationSettings.NormalVectorColor.B);
    private readonly Color _surfaceColor = new(
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
                if (VisualizeGeometries.Count == 0) return;
                
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

                if (_drawSurface && _surfaceBuffer.IsValid())
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

                if (_drawMeshGrid && _meshGridBuffer.IsValid())
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
                        if (buffer.IsValid())
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
            }
            catch (Exception exception)
            {
                Trace.TraceError($"Error in MeshVisualizationServer: {exception.Message}");
            }
        }
    }

    private void MapGeometryBuffer()
    {
        if (VisualizeGeometries.Count == 0) return;
        
        try
        {
            // Use the new methods that handle multiple meshes
            RenderHelper.MapSurfaceBuffer(_surfaceBuffer, VisualizeGeometries, _extrusion);
            RenderHelper.MapMeshGridBuffer(_meshGridBuffer, VisualizeGeometries, _extrusion);
            
            // Handle normal vectors for all meshes
            MapNormalsBuffer();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error mapping geometry buffer in MeshVisualizationServer: {ex}");
        }
    }

    private void MapNormalsBuffer()
    {
        // Clear existing normal buffers
        _normalBuffers = [];
        
        // Create a buffer for each vertex of each mesh
        foreach (var mesh in VisualizeGeometries)
        {
            var area = RenderGeometryHelper.ComputeMeshSurfaceArea(mesh);
            var offset = RenderGeometryHelper.InterpolateOffsetByArea(area);
            var normalLength = RenderGeometryHelper.InterpolateAxisLengthByArea(area);

            for (var i = 0; i < mesh.Vertices.Count; i++)
            {
                var vertex = mesh.Vertices[i];
                var normal = RenderGeometryHelper.GetMeshVertexNormal(mesh, i, mesh.DistributionOfNormals);
                
                var buffer = new RenderingBufferStorage();
                RenderHelper.MapNormalVectorBuffer(buffer, vertex + normal * (offset + _extrusion), normal, normalLength);
                _normalBuffers = _normalBuffers.Append(buffer).ToArray();
            }
        }
    }

    private void UpdateEffects()
    {
        if (_surfaceBuffer.FormatBits == 0)
            _surfaceBuffer.FormatBits = VertexFormatBits.PositionNormal;
            
        if (_meshGridBuffer.FormatBits == 0)
            _meshGridBuffer.FormatBits = VertexFormatBits.Position;
            
        _surfaceBuffer.EffectInstance ??= new EffectInstance(_surfaceBuffer.FormatBits);
        _meshGridBuffer.EffectInstance ??= new EffectInstance(_meshGridBuffer.FormatBits);

        _surfaceBuffer.EffectInstance.SetColor(_surfaceColor);
        _meshGridBuffer.EffectInstance.SetColor(_meshColor);
        _surfaceBuffer.EffectInstance.SetTransparency(_transparency);

        foreach (var normalBuffer in _normalBuffers)
        {
            if (normalBuffer.FormatBits == 0)
                normalBuffer.FormatBits = VertexFormatBits.Position;
                
            normalBuffer.EffectInstance ??= new EffectInstance(normalBuffer.FormatBits);
            normalBuffer.EffectInstance.SetColor(_normalColor);
        }
    }

    protected override void DisposeBuffers()
    {
        _surfaceBuffer.Dispose();
        _meshGridBuffer.Dispose();
        
        foreach (var buffer in _normalBuffers)
        {
            buffer.Dispose();
        }
        _normalBuffers = [];
    }
}
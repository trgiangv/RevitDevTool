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
    private readonly List<RenderingBufferStorage[]> _normalBuffers = [];
    private readonly List<RenderingBufferStorage> _surfaceBuffers = [];
    private readonly List<RenderingBufferStorage> _meshGridBuffers = [];

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
                
                if (HasGeometryUpdates || _surfaceBuffers.Count == 0 || _meshGridBuffers.Count == 0)
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

                if (_drawNormalVector)
                {
                    foreach (var buffers in _normalBuffers)
                    {
                        if (!buffers.Any()) continue;

                        foreach (var buffer in buffers)
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
        _surfaceBuffers.Clear(true);
        _meshGridBuffers.Clear(true);

        if (VisualizeGeometries.Count == 0) return;
        
        try
        {
            foreach (var visualizeGeometry in VisualizeGeometries)
            {
                var surfaceBuffer = new RenderingBufferStorage();
                RenderHelper.MapSurfaceBuffer(surfaceBuffer, visualizeGeometry, _extrusion);
                _surfaceBuffers.Add(surfaceBuffer);

                var meshGridBuffer = new RenderingBufferStorage();
                RenderHelper.MapMeshGridBuffer(meshGridBuffer, visualizeGeometry, _extrusion);
                _meshGridBuffers.Add(meshGridBuffer);
            }

            MapNormalsBuffer();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error mapping geometry buffer in MeshVisualizationServer: {ex}");
        }
    }

    private void MapNormalsBuffer()
    {
        _normalBuffers.Clear(true);

        foreach (var mesh in VisualizeGeometries)
        {
            var area = RenderGeometryHelper.ComputeMeshSurfaceArea(mesh);
            var offset = RenderGeometryHelper.InterpolateOffsetByArea(area);
            var normalLength = RenderGeometryHelper.InterpolateAxisLengthByArea(area);

            var normals = Enumerable.Range(0, mesh.Vertices.Count)
                .Select(_ => new RenderingBufferStorage())
                .ToArray();
            
            for (var i = 0; i < mesh.Vertices.Count; i++)
            {
                var vertex = mesh.Vertices[i];
                var buffer = normals[i];
                var normal = RenderGeometryHelper.GetMeshVertexNormal(mesh, i, mesh.DistributionOfNormals);
                RenderHelper.MapNormalVectorBuffer(buffer, vertex + normal * (offset + _extrusion), normal, normalLength);
            }

            _normalBuffers.Add(normals);
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
            foreach (var buffer in normalBuffer)
            {
                buffer.EffectInstance ??= new EffectInstance(buffer.FormatBits);
                buffer.EffectInstance.SetColor(_normalColor);
            }
        }
    }

    protected override void DisposeBuffers()
    {
        _surfaceBuffers.Clear(true);
        _meshGridBuffers.Clear(true);
        _normalBuffers.Clear(true);
    }
}
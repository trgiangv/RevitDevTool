using System.Diagnostics;
using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using RevitDevTool.Visualization.Server.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class FaceVisualizationServer : VisualizationServer<Face>
{
    private readonly Guid _serverId = new("F67DFF33-B5A8-47AA-AB81-557A032C001E");
    public override Guid GetServerId() => _serverId;
    private readonly RenderingBufferStorage _meshGridBuffer = new();
    private readonly RenderingBufferStorage _normalBuffer = new();
    private readonly RenderingBufferStorage _surfaceBuffer = new();

    private readonly double _extrusion = FaceVisualizationSettings.Extrusion;
    private readonly double _transparency = FaceVisualizationSettings.Transparency;

    private readonly Color _meshColor = new(
        FaceVisualizationSettings.MeshColor.R,
        FaceVisualizationSettings.MeshColor.G,
        FaceVisualizationSettings.MeshColor.B);
    private readonly Color _normalColor = new(
        FaceVisualizationSettings.NormalVectorColor.R, 
        FaceVisualizationSettings.NormalVectorColor.G, 
        FaceVisualizationSettings.NormalVectorColor.B);
    private readonly Color _surfaceColor = new(
        FaceVisualizationSettings.SurfaceColor.R, 
        FaceVisualizationSettings.SurfaceColor.G, 
        FaceVisualizationSettings.SurfaceColor.B);

    private readonly bool _drawMeshGrid = FaceVisualizationSettings.ShowMeshGrid;
    private readonly bool _drawNormalVector = FaceVisualizationSettings.ShowNormalVector;
    private readonly bool _drawSurface = FaceVisualizationSettings.ShowSurface;
    
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
            try
            {
                if (VisualizeGeometries.Count == 0) return;
                
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

                if (_drawNormalVector && _normalBuffer.IsValid())
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
                Trace.TraceError($"Error in FaceVisualizationServer: {exception.Message}");
            }
        }
    }

    private void MapGeometryBuffer()
    {
        if (VisualizeGeometries.Count == 0) return;
        
        try
        {
            // Collect meshes from all faces
            var meshes = RenderHelper.CollectMeshesFromFaces(VisualizeGeometries);
            
            // Map surface and mesh grid buffers using the collected meshes
            RenderHelper.MapSurfaceBuffer(_surfaceBuffer, meshes, _extrusion);
            RenderHelper.MapMeshGridBuffer(_meshGridBuffer, meshes, _extrusion);
            
            // Collect normal data from all faces
            var normalData = RenderHelper.CollectFaceNormalData(VisualizeGeometries, _extrusion);
            
            // Map normal vectors
            RenderHelper.MapNormalVectorsForFaces(_normalBuffer, normalData);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error mapping geometry buffer in FaceVisualizationServer: {ex}");
        }
    }

    private void UpdateEffects()
    {
        if (_surfaceBuffer.FormatBits == 0)
            _surfaceBuffer.FormatBits = VertexFormatBits.PositionNormal;
            
        if (_meshGridBuffer.FormatBits == 0)
            _meshGridBuffer.FormatBits = VertexFormatBits.Position;
            
        if (_normalBuffer.FormatBits == 0)
            _normalBuffer.FormatBits = VertexFormatBits.Position;
            
        _surfaceBuffer.EffectInstance ??= new EffectInstance(_surfaceBuffer.FormatBits);
        _meshGridBuffer.EffectInstance ??= new EffectInstance(_meshGridBuffer.FormatBits);
        _normalBuffer.EffectInstance ??= new EffectInstance(_normalBuffer.FormatBits);

        _surfaceBuffer.EffectInstance.SetColor(_surfaceColor);
        _meshGridBuffer.EffectInstance.SetColor(_meshColor);
        _normalBuffer.EffectInstance.SetColor(_normalColor);
        _surfaceBuffer.EffectInstance.SetTransparency(_transparency);
    }

    protected override void DisposeBuffers()
    {
        _surfaceBuffer.Dispose();
        _meshGridBuffer.Dispose();
        _normalBuffer.Dispose();
    }
}
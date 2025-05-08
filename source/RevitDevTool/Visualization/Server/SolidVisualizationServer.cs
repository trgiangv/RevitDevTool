using System.Diagnostics;
using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using RevitDevTool.Visualization.Server.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class SolidVisualizationServer : VisualizationServer<Solid>
{
    private readonly Guid _serverId = new("02B1803B-9008-427E-985F-4ED4DA839EF0");
    public override Guid GetServerId() => _serverId;
    private readonly List<RenderingBufferStorage> _faceBuffers = new(4);
    private readonly List<RenderingBufferStorage> _edgeBuffers = new(8);

    private readonly double _transparency = SolidVisualizationSettings.Transparency;
    private readonly double _scale = SolidVisualizationSettings.Scale;

    private readonly Color _faceColor = new(
        SolidVisualizationSettings.FaceColor.R,
        SolidVisualizationSettings.FaceColor.G,
        SolidVisualizationSettings.FaceColor.B);
    private readonly Color _edgeColor = new(
        SolidVisualizationSettings.EdgeColor.R,
        SolidVisualizationSettings.EdgeColor.G,
        SolidVisualizationSettings.EdgeColor.B);

    private readonly bool _drawFace = SolidVisualizationSettings.ShowFace;
    private readonly bool _drawEdge = SolidVisualizationSettings.ShowEdge;
    
    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawFace && _transparency > 0;

    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view)
    {
        if (VisualizeGeometries.Count == 0) return null;
        List<XYZ> minPoints = [];
        List<XYZ> maxPoints = [];
        
        foreach (var solid in VisualizeGeometries)
        {
            var boundingBox = solid.GetBoundingBox();
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
                if (HasGeometryUpdates)
                {
                    MapGeometryBuffer();
                    HasGeometryUpdates = false;
                }

                if (HasEffectsUpdates)
                {
                    UpdateEffects();
                    HasEffectsUpdates = false;
                }

                if (_drawFace)
                {
                    var isTransparentPass = DrawContext.IsTransparentPass();
                    if (isTransparentPass && _transparency > 0 || !isTransparentPass && _transparency == 0)
                    {
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
                }

                if (_drawEdge)
                {
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
            }
            catch (Exception exception)
            {
                Trace.TraceError($"Error in SolidVisualizationServer: {exception.Message}");
            }
        }
    }

    private void MapGeometryBuffer()
    {
        foreach (var solid in VisualizeGeometries)
        {
            var scaledSolid = RenderGeometryHelper.ScaleSolid(solid, _scale);

            var faceIndex = 0;
            foreach (Face face in scaledSolid.Faces)
            {
                var buffer = CreateOrUpdateBuffer(_faceBuffers, faceIndex++);
                MapFaceBuffers(buffer, face);
            }

            var edgeIndex = 0;
            foreach (Edge edge in scaledSolid.Edges)
            {
                var buffer = CreateOrUpdateBuffer(_edgeBuffers, edgeIndex++);
                MapEdgeBuffers(buffer, edge);
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

    private static RenderingBufferStorage CreateOrUpdateBuffer(List<RenderingBufferStorage> buffers, int index)
    {
        RenderingBufferStorage buffer;
        if (buffers.Count > index)
        {
            buffer = buffers[index];
        }
        else
        {
            buffer = new RenderingBufferStorage();
            buffers.Add(buffer);
        }

        return buffer;
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
}
using System.Diagnostics;
using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using RevitDevTool.Visualization.Server.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class PolylineVisualizationServer : VisualizationServer<GeometryObject>
{
    private readonly Guid _serverId = new("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");
    public override Guid GetServerId() => _serverId;
    
    private readonly List<RenderingBufferStorage> _surfaceBuffers = [];
    private readonly List<RenderingBufferStorage> _curveBuffers = [];
    private readonly List<RenderingBufferStorage> _normalsBuffers = [];
    
    private readonly double _transparency = PolylineVisualizationSettings.Transparency;
    private readonly double _diameter = PolylineVisualizationSettings.Diameter.FromMillimeters();

    private readonly Color _surfaceColor = new(
        PolylineVisualizationSettings.SurfaceColor.R,
        PolylineVisualizationSettings.SurfaceColor.G,
        PolylineVisualizationSettings.SurfaceColor.B);
    private readonly Color _curveColor = new(
        PolylineVisualizationSettings.CurveColor.R,
        PolylineVisualizationSettings.CurveColor.G,
        PolylineVisualizationSettings.CurveColor.B);
    private readonly Color _directionColor = new(
        PolylineVisualizationSettings.DirectionColor.R,
        PolylineVisualizationSettings.DirectionColor.G,
        PolylineVisualizationSettings.DirectionColor.B);

    private readonly bool _drawCurve = PolylineVisualizationSettings.ShowCurve;
    private readonly bool _drawDirection = PolylineVisualizationSettings.ShowDirection;
    private readonly bool _drawSurface = PolylineVisualizationSettings.ShowSurface;
    
    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawSurface && _transparency > 0;
    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view) => null;

    public override void RenderScene(Autodesk.Revit.DB.View view, DisplayStyle displayStyle)
    {
        lock (RenderLock)
        {
            try
            {
                if (VisualizeGeometries.Count == 0) return;
                
                if (HasGeometryUpdates || _surfaceBuffers.Count == 0 || _curveBuffers.Count == 0)
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
                    if (isTransparentPass && _transparency > 0 || !isTransparentPass && _transparency == 0)
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

                if (_drawCurve && _curveBuffers.Count != 0)
                {
                    foreach (var curveBuffer in _curveBuffers)
                    {
                        DrawContext.FlushBuffer(curveBuffer.VertexBuffer,
                            curveBuffer.VertexBufferCount,
                            curveBuffer.IndexBuffer,
                            curveBuffer.IndexBufferCount,
                            curveBuffer.VertexFormat,
                            curveBuffer.EffectInstance, PrimitiveType.LineList, 0,
                            curveBuffer.PrimitiveCount);
                    }
                }

                if (_drawDirection)
                {
                    foreach (var buffer in _normalsBuffers)
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
                Trace.TraceError($"Error in PolylineVisualizationServer: {exception.Message}");
            }
        }
    }

    private void MapGeometryBuffer()
    {
        if (VisualizeGeometries.Count == 0) return;
        
        try
        {
            foreach (var visualizeGeometry in VisualizeGeometries)
            {
                var vertices = visualizeGeometry switch
                {
                    Edge edge => edge.Tessellate(),
                    Curve curve => curve.Tessellate(),
                    PolyLine polyLine => polyLine.GetCoordinates(),
                    _ => throw new ArgumentException("Unsupported geometry type for polyline visualization",
                        nameof(visualizeGeometry))
                };
                
                var surfaceBuffer = new RenderingBufferStorage();
                RenderHelper.MapCurveSurfaceBuffer(surfaceBuffer, vertices, _diameter);
                _surfaceBuffers.Add(surfaceBuffer);
                
                var curveBuffer = new RenderingBufferStorage();
                RenderHelper.MapCurveBuffer(curveBuffer, vertices, _diameter);
                _curveBuffers.Add(curveBuffer);
            }
            MapDirectionsBuffer();
        }
        catch (Exception ex)    
        {
            Trace.TraceError($"Error mapping geometry buffer in PolylineVisualizationServer: {ex}");
        }
    }

    private void MapDirectionsBuffer()
    {
        if (VisualizeGeometries.Count == 0) return;
        
        foreach (var visualizeGeometry in VisualizeGeometries)
        {
            var vertices = visualizeGeometry switch
            {
                Edge edge => edge.Tessellate(),
                Curve curve => curve.Tessellate(),
                PolyLine polyLine => polyLine.GetCoordinates(),
                _ => throw new ArgumentException("Unsupported geometry type for polyline visualization",
                    nameof(visualizeGeometry))
            };
            
            if (vertices.Count < 2) continue;

            var verticalOffset = 0d;
            for (var i = 0; i < vertices.Count - 1; i++)
            {
                var startPoint = vertices[i];
                var endPoint = vertices[i + 1];
                var centerPoint = (startPoint + endPoint) / 2;
                var buffer = new RenderingBufferStorage();

                var segmentVector = endPoint - startPoint;
                var segmentLength = segmentVector.GetLength();
                var segmentDirection = segmentVector.Normalize();
                if (verticalOffset == 0)
                {
                    verticalOffset = RenderGeometryHelper.InterpolateOffsetByDiameter(_diameter) + _diameter / 2d;
                }

                var offsetVector = XYZ.BasisX.CrossProduct(segmentDirection).Normalize() * verticalOffset;
                if (offsetVector.IsZeroLength())
                {
                    offsetVector = XYZ.BasisY.CrossProduct(segmentDirection).Normalize() * verticalOffset;
                }

                if (offsetVector.Z < 0)
                {
                    offsetVector = -offsetVector;
                }

                var arrowLength = segmentLength > 1 ? 1d : segmentLength * 0.6;
                var arrowOrigin = centerPoint + offsetVector - segmentDirection * (arrowLength / 2);

                RenderHelper.MapNormalVectorBuffer(buffer, arrowOrigin, segmentDirection, arrowLength);
                _normalsBuffers.Add(buffer);
            }
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

        foreach (var curveBuffer in _curveBuffers)
        {
            curveBuffer.EffectInstance ??= new EffectInstance(curveBuffer.FormatBits);
            curveBuffer.EffectInstance.SetColor(_curveColor);
        }

        foreach (var buffer in _normalsBuffers)
        {
            buffer.EffectInstance ??= new EffectInstance(buffer.FormatBits);
            buffer.EffectInstance.SetColor(_directionColor);
        }
    }

    protected override void DisposeBuffers()
    {
        _surfaceBuffers.Clear();
        _curveBuffers.Clear();
        _normalsBuffers.Clear();
    }
}
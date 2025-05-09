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
    private readonly RenderingBufferStorage _surfaceBuffer = new();
    private readonly RenderingBufferStorage _curveBuffer = new();
    private readonly List<RenderingBufferStorage> _normalsBuffers = new(1);

    private List<XYZ> InputPoints => TessellationHelper.GetXyz(VisualizeGeometries);

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
                
                if (HasGeometryUpdates || !_surfaceBuffer.IsValid() || !_curveBuffer.IsValid())
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

                if (_drawCurve && _curveBuffer.IsValid())
                {
                    DrawContext.FlushBuffer(_curveBuffer.VertexBuffer,
                        _curveBuffer.VertexBufferCount,
                        _curveBuffer.IndexBuffer,
                        _curveBuffer.IndexBufferCount,
                        _curveBuffer.VertexFormat,
                        _curveBuffer.EffectInstance, PrimitiveType.LineList, 0,
                        _curveBuffer.PrimitiveCount);
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
        if (InputPoints.Count == 0) return;
        
        try
        {
            DisposeBuffers();
            RenderHelper.MapCurveSurfaceBuffer(_surfaceBuffer, InputPoints, _diameter);
            RenderHelper.MapCurveBuffer(_curveBuffer, InputPoints, _diameter);
            MapDirectionsBuffer();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error mapping geometry buffer in PolylineVisualizationServer: {ex}");
        }
    }

    private void MapDirectionsBuffer()
    {
        // Clear existing buffers to prevent stale data
        _normalsBuffers.Clear();
        
        // Check if we have at least 2 points to create a direction
        if (InputPoints.Count < 2)
            return;
            
        var verticalOffset = 0d;

        for (var i = 0; i < InputPoints.Count - 1; i++)
        {
            var startPoint = InputPoints[i];
            var endPoint = InputPoints[i + 1];
            var centerPoint = (startPoint + endPoint) / 2;
            var buffer = new RenderingBufferStorage();
            _normalsBuffers.Add(buffer);

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
        }
    }

    private void UpdateEffects()
    {
        if (_surfaceBuffer.FormatBits == 0)
            _surfaceBuffer.FormatBits = VertexFormatBits.PositionNormal;
            
        _surfaceBuffer.EffectInstance ??= new EffectInstance(_surfaceBuffer.FormatBits);
        _surfaceBuffer.EffectInstance.SetColor(_surfaceColor);
        _surfaceBuffer.EffectInstance.SetTransparency(_transparency);

        if (_curveBuffer.FormatBits == 0)
            _curveBuffer.FormatBits = VertexFormatBits.Position;
            
        _curveBuffer.EffectInstance ??= new EffectInstance(_curveBuffer.FormatBits);
        _curveBuffer.EffectInstance.SetColor(_curveColor);

        foreach (var buffer in _normalsBuffers)
        {
            if (buffer.FormatBits == 0)
                buffer.FormatBits = VertexFormatBits.Position;
                
            buffer.EffectInstance ??= new EffectInstance(buffer.FormatBits);
            buffer.EffectInstance.SetColor(_directionColor);
        }
    }

    protected override void DisposeBuffers()
    {
        _surfaceBuffer.Dispose();
        _curveBuffer.Dispose();
        
        foreach (var buffer in _normalsBuffers)
        {
            buffer.Dispose();
        }
        _normalsBuffers.Clear();
    }
}
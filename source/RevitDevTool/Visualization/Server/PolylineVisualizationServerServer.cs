using System.Diagnostics;
using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Extensions ;
using RevitDevTool.Models.Config ;
using RevitDevTool.Visualization.Contracts ;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class PolylineVisualizationServerServer : VisualizationServerServer<GeometryObject>
{
    private readonly Guid _serverId = new("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");
    public override Guid GetServerId() => _serverId;
    
    private readonly List<RenderingBufferStorage> _surfaceBuffers = [];
    private readonly List<RenderingBufferStorage> _curveBuffers = [];
    private readonly List<RenderingBufferStorage> _normalsBuffers = [];
    
    private double _transparency = PolylineVisualizationSettings.Default.Transparency;
    private double _diameter = PolylineVisualizationSettings.Default.Diameter;

    private Color _surfaceColor = new(
        PolylineVisualizationSettings.Default.SurfaceColor.R,
        PolylineVisualizationSettings.Default.SurfaceColor.G,
        PolylineVisualizationSettings.Default.SurfaceColor.B
        );
    private Color _curveColor = new(
        PolylineVisualizationSettings.Default.CurveColor.R,
        PolylineVisualizationSettings.Default.CurveColor.G,
        PolylineVisualizationSettings.Default.CurveColor.B
        );
    private Color _directionColor = new(
        PolylineVisualizationSettings.Default.DirectionColor.R,
        PolylineVisualizationSettings.Default.DirectionColor.G,
        PolylineVisualizationSettings.Default.DirectionColor.B
        );

    private bool _drawCurve = PolylineVisualizationSettings.Default.ShowCurve;
    private bool _drawDirection = PolylineVisualizationSettings.Default.ShowDirection;
    private bool _drawSurface = PolylineVisualizationSettings.Default.ShowSurface;
    
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
        _surfaceBuffers.Clear(true);
        _curveBuffers.Clear(true);

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
        _normalsBuffers.Clear(true);

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

    public override void UpdateEffects()
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

    public override void UpdateCurveColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _curveColor = value;
            HasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public override void UpdateDirectionColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _directionColor = value;
            HasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public override void UpdateDiameter(double value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _diameter = value;
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

    public override void UpdateCurveVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawCurve = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public override void UpdateDirectionVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawDirection = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    protected override void DisposeBuffers()
    {
        _surfaceBuffers.Clear(true);
        _curveBuffers.Clear(true);
        _normalsBuffers.Clear(true);
    }
}
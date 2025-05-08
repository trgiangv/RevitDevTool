using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using RevitDevTool.Visualization.Server.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class PolylineVisualizationServer : VisualizationServer<XYZ>
{
    private readonly RenderingBufferStorage _surfaceBuffer = new();
    private readonly RenderingBufferStorage _curveBuffer = new();
    private readonly List<RenderingBufferStorage> _normalsBuffers = new(1);

    private double _transparency = PolylineVisualizationSettings.Transparency;
    private double _diameter = PolylineVisualizationSettings.Diameter;

    private Color _surfaceColor = new(
        PolylineVisualizationSettings.SurfaceColor.R,
        PolylineVisualizationSettings.SurfaceColor.G,
        PolylineVisualizationSettings.SurfaceColor.B);
    private Color _curveColor = new(
        PolylineVisualizationSettings.CurveColor.R,
        PolylineVisualizationSettings.CurveColor.G,
        PolylineVisualizationSettings.CurveColor.B);
    private Color _directionColor = new(
        PolylineVisualizationSettings.DirectionColor.R,
        PolylineVisualizationSettings.DirectionColor.G,
        PolylineVisualizationSettings.DirectionColor.B);

    private bool _drawCurve = PolylineVisualizationSettings.ShowCurve;
    private bool _drawDirection = PolylineVisualizationSettings.ShowDirection;
    private bool _drawSurface = PolylineVisualizationSettings.ShowSurface;
    
    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawSurface && _transparency > 0;
    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view) => null;

    public override void RenderScene(Autodesk.Revit.DB.View view, DisplayStyle displayStyle)
    {
        lock (RenderLock)
        {
            try
            {
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

                if (_drawCurve)
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
        RenderHelper.MapCurveSurfaceBuffer(_surfaceBuffer, VisualizeGeometries, _diameter);
        RenderHelper.MapCurveBuffer(_curveBuffer, VisualizeGeometries, _diameter);
        MapDirectionsBuffer();
    }

    private void MapDirectionsBuffer()
    {
        var verticalOffset = 0d;

        for (var i = 0; i < VisualizeGeometries.Count - 1; i++)
        {
            var startPoint = VisualizeGeometries[i];
            var endPoint = VisualizeGeometries[i + 1];
            var centerPoint = (startPoint + endPoint) / 2;
            var buffer = CreateNormalBuffer(i);

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


    private RenderingBufferStorage CreateNormalBuffer(int vertexIndex)
    {
        RenderingBufferStorage buffer;
        if (_normalsBuffers.Count > vertexIndex)
        {
            buffer = _normalsBuffers[vertexIndex];
        }
        else
        {
            buffer = new RenderingBufferStorage();
            _normalsBuffers.Add(buffer);
        }

        return buffer;
    }

    private void UpdateEffects()
    {
        _surfaceBuffer.EffectInstance ??= new EffectInstance(_surfaceBuffer.FormatBits);
        _surfaceBuffer.EffectInstance.SetColor(_surfaceColor);
        _surfaceBuffer.EffectInstance.SetTransparency(_transparency);

        _curveBuffer.EffectInstance ??= new EffectInstance(_curveBuffer.FormatBits);
        _curveBuffer.EffectInstance.SetColor(_curveColor);

        foreach (var buffer in _normalsBuffers)
        {
            buffer.EffectInstance ??= new EffectInstance(buffer.FormatBits);
            buffer.EffectInstance.SetColor(_directionColor);
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

    public void UpdateCurveColor(Color value)
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

    public void UpdateDirectionColor(Color value)
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

    public void UpdateDiameter(double value)
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

    public void UpdateCurveVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawCurve = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateDirectionVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawDirection = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }
    
    public event EventHandler<RenderFailedEventArgs>? RenderFailed;
}
using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Extensions;
using RevitDevTool.Services;
using RevitDevTool.Visualization.Contracts;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using System.Diagnostics;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class PolylineVisualizationServer : VisualizationServer<GeometryObject>
{
    private readonly Guid _serverId = new("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");
    public override Guid GetServerId() => _serverId;

    private readonly List<RenderingBufferStorage> _surfaceBuffers = [];
    private readonly List<RenderingBufferStorage> _curveBuffers = [];
    private readonly List<RenderingBufferStorage> _normalsBuffers = [];

    private double _transparency = SettingsService.Instance.VisualizationConfig.PolylineSettings.Transparency;
    private double _diameter = SettingsService.Instance.VisualizationConfig.PolylineSettings.Diameter;

    private Color _surfaceColor = new(
        SettingsService.Instance.VisualizationConfig.PolylineSettings.SurfaceColor.R,
        SettingsService.Instance.VisualizationConfig.PolylineSettings.SurfaceColor.G,
        SettingsService.Instance.VisualizationConfig.PolylineSettings.SurfaceColor.B
        );
    private Color _curveColor = new(
        SettingsService.Instance.VisualizationConfig.PolylineSettings.CurveColor.R,
        SettingsService.Instance.VisualizationConfig.PolylineSettings.CurveColor.G,
        SettingsService.Instance.VisualizationConfig.PolylineSettings.CurveColor.B
        );
    private Color _directionColor = new(
        SettingsService.Instance.VisualizationConfig.PolylineSettings.DirectionColor.R,
        SettingsService.Instance.VisualizationConfig.PolylineSettings.DirectionColor.G,
        SettingsService.Instance.VisualizationConfig.PolylineSettings.DirectionColor.B
        );

    private bool _drawCurve = SettingsService.Instance.VisualizationConfig.PolylineSettings.ShowCurve;
    private bool _drawDirection = SettingsService.Instance.VisualizationConfig.PolylineSettings.ShowDirection;
    private bool _drawSurface = SettingsService.Instance.VisualizationConfig.PolylineSettings.ShowSurface;

    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawSurface && _transparency > 0;
    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view) => null;

    protected override void RenderScene()
    {
        if (visualizeGeometries.Count == 0) return;

        if (hasGeometryUpdates || _surfaceBuffers.Count == 0 || _curveBuffers.Count == 0)
        {
            MapGeometryBuffer();
            hasGeometryUpdates = false;
        }

        if (hasEffectsUpdates)
        {
            UpdateEffects();
            hasEffectsUpdates = false;
        }

        RenderSurfaceBuffers();
        RenderCurveBuffers();
        RenderDirectionBuffers();
    }

    private void RenderSurfaceBuffers()
    {
        if (!_drawSurface || _surfaceBuffers.Count == 0) return;
        if (!ShouldRenderTransparentPass(_transparency)) return;

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

    private void RenderCurveBuffers()
    {
        if (!_drawCurve || _curveBuffers.Count == 0) return;

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

    private void RenderDirectionBuffers()
    {
        if (!_drawDirection) return;

        foreach (var buffer in _normalsBuffers.Where(b => b.IsValid()))
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

    private void MapGeometryBuffer()
    {
        DisposeBuffers();

        if (visualizeGeometries.Count == 0) return;

        try
        {
            foreach (var visualizeGeometry in visualizeGeometries)
            {
                var vertices = GetVertices(visualizeGeometry);

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

        if (visualizeGeometries.Count == 0) return;

        foreach (var visualizeGeometry in visualizeGeometries)
        {
            var vertices = GetVertices(visualizeGeometry);
            if (vertices.Count < 2) continue;

            MapDirectionBuffersForVertices(vertices);
        }
    }

    private void MapDirectionBuffersForVertices(IList<XYZ> vertices)
    {
        var verticalOffset = RenderGeometryHelper.InterpolateOffsetByDiameter(_diameter) + (_diameter / 2d);

        for (var i = 0; i < vertices.Count - 1; i++)
        {
            var startPoint = vertices[i];
            var endPoint = vertices[i + 1];
            var buffer = CreateDirectionBuffer(startPoint, endPoint, verticalOffset);
            _normalsBuffers.Add(buffer);
        }
    }

    private static RenderingBufferStorage CreateDirectionBuffer(XYZ startPoint, XYZ endPoint, double verticalOffset)
    {
        var buffer = new RenderingBufferStorage();
        var centerPoint = (startPoint + endPoint) / 2;
        var segmentVector = endPoint - startPoint;
        var segmentLength = segmentVector.GetLength();
        var segmentDirection = segmentVector.Normalize();

        var offsetVector = ComputeOffsetVector(segmentDirection, verticalOffset);
        var arrowLength = segmentLength > 1 ? 1d : segmentLength * 0.6;
        var arrowOrigin = centerPoint + offsetVector - (segmentDirection * (arrowLength / 2));

        RenderHelper.MapNormalVectorBuffer(buffer, arrowOrigin, segmentDirection, arrowLength);
        return buffer;
    }

    private static XYZ ComputeOffsetVector(XYZ segmentDirection, double verticalOffset)
    {
        var offsetVector = XYZ.BasisX.CrossProduct(segmentDirection).Normalize() * verticalOffset;

        if (offsetVector.IsZeroLength())
            offsetVector = XYZ.BasisY.CrossProduct(segmentDirection).Normalize() * verticalOffset;

        return offsetVector.Z < 0 ? -offsetVector : offsetVector;
    }

    private static IList<XYZ> GetVertices(GeometryObject geometry)
    {
        return geometry switch
        {
            Edge edge => edge.Tessellate(),
            Curve curve => curve.Tessellate(),
            PolyLine polyLine => polyLine.GetCoordinates(),
            _ => throw new ArgumentException("Unsupported geometry type for polyline visualization", nameof(geometry))
        };
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

    public void UpdateSurfaceColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _surfaceColor = value;
            hasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateCurveColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _curveColor = value;
            hasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateDirectionColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _directionColor = value;
            hasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateDiameter(double value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _diameter = value;
            hasGeometryUpdates = true;
            hasEffectsUpdates = true;
            DisposeBuffers();

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateTransparency(double value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _transparency = value;
            hasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateSurfaceVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _drawSurface = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateCurveVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _drawCurve = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateDirectionVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
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
using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using RevitDevTool.Visualization.Server.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class XyzVisualizationServer : VisualizationServer<XYZ>
{
    private readonly RenderingBufferStorage[] _planeBuffers = Enumerable.Range(0, 3)
        .Select(_ => new RenderingBufferStorage())
        .ToArray();

    private readonly RenderingBufferStorage[] _axisBuffers = Enumerable.Range(0, 3)
        .Select(_ => new RenderingBufferStorage())
        .ToArray();

    private readonly XYZ[] _normals =
    [
        XYZ.BasisX,
        XYZ.BasisY,
        XYZ.BasisZ
    ];

    private double _transparency = XyzVisualizationSettings.Transparency;
    private double _axisLength = XyzVisualizationSettings.AxisLength;

    private Color _xColor = new(
        XyzVisualizationSettings.XColor.R,
        XyzVisualizationSettings.XColor.G,
        XyzVisualizationSettings.XColor.B);
    private Color _yColor = new(
        XyzVisualizationSettings.YColor.R,
        XyzVisualizationSettings.YColor.G,
        XyzVisualizationSettings.YColor.B);
    private Color _zColor = new(
        XyzVisualizationSettings.ZColor.R,
        XyzVisualizationSettings.ZColor.G,
        XyzVisualizationSettings.ZColor.B);

    private bool _drawPlane = XyzVisualizationSettings.ShowPlane;
    private bool _drawXAxis = XyzVisualizationSettings.ShowXAxis;
    private bool _drawYAxis = XyzVisualizationSettings.ShowYAxis;
    private bool _drawZAxis = XyzVisualizationSettings.ShowZAxis;
    
    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawPlane && _transparency > 0;

    public override Outline GetBoundingBox(Autodesk.Revit.DB.View view)
    {
        var minPoint = new XYZ(VisualizeGeometries.Min(p => p.X) - _axisLength, VisualizeGeometries.Min(p => p.Y) - _axisLength, VisualizeGeometries.Min(p => p.Z) - _axisLength);
        var maxPoint = new XYZ(VisualizeGeometries.Max(p => p.X) + _axisLength, VisualizeGeometries.Max(p => p.Y) + _axisLength, VisualizeGeometries.Max(p => p.Z) + _axisLength);

        return new Outline(minPoint, maxPoint);
    }

    public override void RenderScene(Autodesk.Revit.DB.View view, DisplayStyle displayStyle)
    {
        lock (RenderLock)
        {
            try
            {
                if (HasGeometryUpdates)
                {
                    UpdateGeometryBuffer();
                    HasGeometryUpdates = false;
                }

                if (HasEffectsUpdates)
                {
                    UpdateEffects();
                    HasEffectsUpdates = false;
                }

                if (_drawXAxis)
                {
                    RenderAxisBuffer(_axisBuffers[0]);
                    RenderPlaneBuffer(_planeBuffers[0]);
                }

                if (_drawYAxis)
                {
                    RenderAxisBuffer(_axisBuffers[1]);
                    RenderPlaneBuffer(_planeBuffers[1]);
                }

                if (_drawZAxis)
                {
                    RenderAxisBuffer(_axisBuffers[2]);
                    RenderPlaneBuffer(_planeBuffers[2]);
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

    private void RenderPlaneBuffer(RenderingBufferStorage buffer)
    {
        if (!_drawPlane) return;

        var isTransparentPass = DrawContext.IsTransparentPass();
        if (isTransparentPass && _transparency > 0 || !isTransparentPass && _transparency == 0)
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

    private void RenderAxisBuffer(RenderingBufferStorage buffer)
    {
        DrawContext.FlushBuffer(buffer.VertexBuffer,
            buffer.VertexBufferCount,
            buffer.IndexBuffer,
            buffer.IndexBufferCount,
            buffer.VertexFormat,
            buffer.EffectInstance, PrimitiveType.LineList, 0,
            buffer.PrimitiveCount);
    }

    private void UpdateGeometryBuffer()
    {
        MapNormalBuffer();
        MapPlaneBuffer();
    }

    private void MapNormalBuffer()
    {
        foreach (var point in VisualizeGeometries)
        {
            var normalExtendLength = _axisLength > 1 ? 0.8 : _axisLength * 0.8;
            for (var i = 0; i < _normals.Length; i++)
            {
                var normal = _normals[i];
                var buffer = _axisBuffers[i];
                RenderHelper.MapNormalVectorBuffer(buffer, point - normal * (_axisLength + normalExtendLength), normal, 2 * (_axisLength + normalExtendLength));
            }
        }
    }

    private void MapPlaneBuffer()
    {
        foreach (var point in VisualizeGeometries)
        {
            for (var i = 0; i < _normals.Length; i++)
            {
                var normal = _normals[i];
                var buffer = _planeBuffers[i];
                RenderHelper.MapSideBuffer(buffer, point - normal * _axisLength, point + normal * _axisLength);
            }
        }
    }

    private void UpdateEffects()
    {
        foreach (var buffer in _planeBuffers)
        {
            buffer.EffectInstance ??= new EffectInstance(buffer.FormatBits);
            buffer.EffectInstance.SetTransparency(_transparency);
        }

        _planeBuffers[0].EffectInstance!.SetColor(_xColor);
        _planeBuffers[1].EffectInstance!.SetColor(_yColor);
        _planeBuffers[2].EffectInstance!.SetColor(_zColor);

        foreach (var buffer in _axisBuffers)
        {
            buffer.EffectInstance ??= new EffectInstance(buffer.FormatBits);
        }

        _axisBuffers[0].EffectInstance!.SetColor(_xColor);
        _axisBuffers[1].EffectInstance!.SetColor(_yColor);
        _axisBuffers[2].EffectInstance!.SetColor(_zColor);
    }

    public void UpdateXColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _xColor = value;
            HasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateYColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _yColor = value;
            HasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateZColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _zColor = value;
            HasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateAxisLength(double value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _axisLength = value;
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

    public void UpdatePlaneVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawPlane = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateXAxisVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawXAxis = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateYAxisVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawYAxis = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateZAxisVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawZAxis = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }
    
    public event EventHandler<RenderFailedEventArgs>? RenderFailed;
}
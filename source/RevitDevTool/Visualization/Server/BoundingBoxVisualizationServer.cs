using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using RevitDevTool.Visualization.Server.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;


public sealed class BoundingBoxVisualizationServer : VisualizationServer<BoundingBoxXYZ>
{
    private readonly RenderingBufferStorage _surfaceBuffer = new();
    private readonly RenderingBufferStorage _edgeBuffer = new();
    private readonly List<RenderingBufferStorage> _axisBuffers = [];
    private readonly XYZ[] _normals =
    [
        XYZ.BasisX,
        XYZ.BasisY,
        XYZ.BasisZ
    ];

    private double _transparency = BoundingBoxVisualizationSettings.Transparency;
    private bool _drawSurface = BoundingBoxVisualizationSettings.ShowSurface;
    private bool _drawEdge = BoundingBoxVisualizationSettings.ShowEdge;
    private bool _drawAxis = BoundingBoxVisualizationSettings.ShowAxis;
    
    private Color _surfaceColor = new(
        BoundingBoxVisualizationSettings.SurfaceColor.R,
        BoundingBoxVisualizationSettings.SurfaceColor.G, 
        BoundingBoxVisualizationSettings.SurfaceColor.B);
    private Color _edgeColor = new(
        BoundingBoxVisualizationSettings.EdgeColor.R, 
        BoundingBoxVisualizationSettings.EdgeColor.G, 
        BoundingBoxVisualizationSettings.EdgeColor.B);
    private Color _axisColor = new(
        BoundingBoxVisualizationSettings.AxisColor.R, 
        BoundingBoxVisualizationSettings.AxisColor.G, 
        BoundingBoxVisualizationSettings.AxisColor.B);

    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawSurface && _transparency > 0;

    public override Outline GetBoundingBox(Autodesk.Revit.DB.View view)
    {
        if (VisualizeGeometries.Count == 0)
            return new Outline(XYZ.Zero, XYZ.Zero);

        var minPoints = VisualizeGeometries
            .Select(box => box.Min)
            .OrderBy(point => point.X)
            .ThenBy(point => point.Y)
            .ThenBy(point => point.Z);
        var maxPoints = VisualizeGeometries
            .Select(box => box.Max)
            .OrderByDescending(point => point.X)
            .ThenByDescending(point => point.Y)
            .ThenByDescending(point => point.Z);
        var minPoint = minPoints.FirstOrDefault();
        var maxPoint = maxPoints.FirstOrDefault();
        if (minPoint is null || maxPoint is null)
            return new Outline(XYZ.Zero, XYZ.Zero);
        
        return new Outline(minPoint, maxPoint);
    }

    public override void RenderScene(Autodesk.Revit.DB.View view, DisplayStyle displayStyle)
    {
        lock (RenderLock)
        {
            try
            {
                if (HasGeometryUpdates || !_surfaceBuffer.IsValid() || !_edgeBuffer.IsValid())
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

                if (_drawEdge)
                {
                    DrawContext.FlushBuffer(_edgeBuffer.VertexBuffer,
                        _edgeBuffer.VertexBufferCount,
                        _edgeBuffer.IndexBuffer,
                        _edgeBuffer.IndexBufferCount,
                        _edgeBuffer.VertexFormat,
                        _edgeBuffer.EffectInstance, PrimitiveType.LineList, 0,
                        _edgeBuffer.PrimitiveCount);
                }

                if (_drawAxis)
                {
                    foreach (var buffer in _axisBuffers)
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
        foreach (var box in VisualizeGeometries)
        {
            RenderHelper.MapBoundingBoxSurfaceBuffer(_surfaceBuffer, box);
            RenderHelper.MapBoundingBoxEdgeBuffer(_edgeBuffer, box);
            MapAxisBuffers(box);
        }
    }

    private void MapAxisBuffers(BoundingBoxXYZ boundingBox)
    {
        var unitVector = new XYZ(1, 1, 1);
        var minPoint = boundingBox.Transform.OfPoint(boundingBox.Min);
        var maxPoint = boundingBox.Transform.OfPoint(boundingBox.Max);
        var axisLength = RenderGeometryHelper.InterpolateAxisLengthByPoints(minPoint, maxPoint);
        
        _axisBuffers.Clear();
        _axisBuffers.AddRange(_normals.Select(_ =>
            new RenderingBufferStorage()));
        for (var i = 0; i < _normals.Length; i++)
        {
            var normal = _normals[i];
            var minBuffer = _axisBuffers[i];
            var maxBuffer = _axisBuffers[i + _normals.Length];

            RenderHelper.MapNormalVectorBuffer(minBuffer, minPoint - unitVector * Context.Application.ShortCurveTolerance, normal, axisLength);
            RenderHelper.MapNormalVectorBuffer(maxBuffer, maxPoint + unitVector * Context.Application.ShortCurveTolerance, -normal, axisLength);
        }
    }

    private void UpdateEffects()
    {
        _surfaceBuffer.EffectInstance ??= new EffectInstance(_surfaceBuffer.FormatBits);
        _surfaceBuffer.EffectInstance.SetColor(_surfaceColor);
        _surfaceBuffer.EffectInstance.SetTransparency(_transparency);

        _edgeBuffer.EffectInstance ??= new EffectInstance(_edgeBuffer.FormatBits);
        _edgeBuffer.EffectInstance.SetColor(_edgeColor);

        foreach (var buffer in _axisBuffers)
        {
            buffer.EffectInstance ??= new EffectInstance(_edgeBuffer.FormatBits);
            buffer.EffectInstance.SetColor(_axisColor);
        }
    }

    public void UpdateSurfaceColor(Color color)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _surfaceColor = color;
            HasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateEdgeColor(Color color)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _edgeColor = color;
            HasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateAxisColor(Color color)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _axisColor = color;
            HasEffectsUpdates = true;

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

    public void UpdateEdgeVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawEdge = visible;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateAxisVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawAxis = visible;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public event EventHandler<RenderFailedEventArgs>? RenderFailed;
}
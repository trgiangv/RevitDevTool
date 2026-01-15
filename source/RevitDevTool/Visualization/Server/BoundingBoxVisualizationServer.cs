using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Settings;
using RevitDevTool.Utils;
using RevitDevTool.Visualization.Contracts;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using System.Diagnostics;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;


public sealed class BoundingBoxVisualizationServer : VisualizationServer<BoundingBoxXYZ>
{
    private readonly Guid _serverId = new("5A67F8D4-30D2-414F-8387-8023C3DAF010");
    public override Guid GetServerId() => _serverId;
    private readonly List<RenderingBufferStorage> _surfaceBuffers = [];
    private readonly List<RenderingBufferStorage> _edgeBuffers = [];
    private readonly List<RenderingBufferStorage> _axisBuffers = [];
    private readonly XYZ[] _normals =
    [
        XYZ.BasisX,
        XYZ.BasisY,
        XYZ.BasisZ
    ];
    private static readonly XYZ UnitVector = new(1, 1, 1);

    private double _transparency;
    private double _scale;
    private bool _drawSurface;
    private bool _drawEdge;
    private bool _drawAxis;
    private Color _surfaceColor;
    private Color _edgeColor;
    private Color _axisColor;

    public BoundingBoxVisualizationServer(ISettingsService settingsService)
    {
        var settings = settingsService.VisualizationConfig.BoundingBoxSettings;
        _transparency = settings.Transparency / 100;
        _scale = settings.Scale / 100;
        _drawSurface = settings.ShowSurface;
        _drawEdge = settings.ShowEdge;
        _drawAxis = settings.ShowAxis;
        _surfaceColor = new Color(settings.SurfaceColor.R, settings.SurfaceColor.G, settings.SurfaceColor.B);
        _edgeColor = new Color(settings.EdgeColor.R, settings.EdgeColor.G, settings.EdgeColor.B);
        _axisColor = new Color(settings.AxisColor.R, settings.AxisColor.G, settings.AxisColor.B);
    }

    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawSurface && _transparency > 0;

    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view)
    {
        if (visualizeGeometries.Count == 0) return null;

        var allTransformedPoints = new List<XYZ>();

        foreach (var box in visualizeGeometries)
        {
            // Generate all 8 corners in local coordinate system and transform them
            XYZ[] localCorners =
            [
                new(box.Min.X, box.Min.Y, box.Min.Z),
                new(box.Max.X, box.Min.Y, box.Min.Z),
                new(box.Max.X, box.Max.Y, box.Min.Z),
                new(box.Min.X, box.Max.Y, box.Min.Z),
                new(box.Min.X, box.Min.Y, box.Max.Z),
                new(box.Max.X, box.Min.Y, box.Max.Z),
                new(box.Max.X, box.Max.Y, box.Max.Z),
                new(box.Min.X, box.Max.Y, box.Max.Z)
            ];

            var transformedCorners = localCorners
                .Select(corner => box.Transform.OfPoint(corner));
            allTransformedPoints.AddRange(transformedCorners);
        }

        // Find actual min/max from all transformed points
        var minX = allTransformedPoints.Min(p => p.X);
        var minY = allTransformedPoints.Min(p => p.Y);
        var minZ = allTransformedPoints.Min(p => p.Z);
        var maxX = allTransformedPoints.Max(p => p.X);
        var maxY = allTransformedPoints.Max(p => p.Y);
        var maxZ = allTransformedPoints.Max(p => p.Z);

        return new Outline(new XYZ(minX, minY, minZ), new XYZ(maxX, maxY, maxZ));
    }

    protected override void RenderScene()
    {
        if (visualizeGeometries.Count == 0) return;

        if (hasGeometryUpdates || _surfaceBuffers.Count == 0 || _edgeBuffers.Count == 0)
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
        RenderEdgeBuffers();
        RenderAxisBuffers();
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

    private void RenderEdgeBuffers()
    {
        if (!_drawEdge || _edgeBuffers.Count == 0) return;

        foreach (var edgeBuffer in _edgeBuffers)
        {
            DrawContext.FlushBuffer(edgeBuffer.VertexBuffer,
                edgeBuffer.VertexBufferCount,
                edgeBuffer.IndexBuffer,
                edgeBuffer.IndexBufferCount,
                edgeBuffer.VertexFormat,
                edgeBuffer.EffectInstance, PrimitiveType.LineList, 0,
                edgeBuffer.PrimitiveCount);
        }
    }

    private void RenderAxisBuffers()
    {
        if (!_drawAxis) return;

        foreach (var buffer in _axisBuffers.Where(b => b.IsValid()))
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
            foreach (var geometry in visualizeGeometries)
            {
                var scaledBox = RenderGeometryHelper.GetScaledBoundingBox(geometry, _scale);
                var surfaceBuffer = new RenderingBufferStorage();
                RenderHelper.MapBoundingBoxSurfaceBuffer(surfaceBuffer, scaledBox);
                _surfaceBuffers.Add(surfaceBuffer);

                var edgeBuffer = new RenderingBufferStorage();
                RenderHelper.MapBoundingBoxEdgeBuffer(edgeBuffer, scaledBox);
                _edgeBuffers.Add(edgeBuffer);

                MapAxisBuffers(scaledBox);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error mapping geometry buffer in BoundingBoxVisualizationServer: {ex}");
        }
    }

    private void MapAxisBuffers(BoundingBoxXYZ box)
    {
        var minPoint = box.Transform.OfPoint(box.Min);
        var maxPoint = box.Transform.OfPoint(box.Max);
        var axisLength = RenderGeometryHelper.InterpolateAxisLengthByPoints(minPoint, maxPoint);

        var axisBuffer = Enumerable.Range(0, 6)
            .Select(_ => new RenderingBufferStorage())
            .ToArray();
        for (var i = 0; i < _normals.Length; i++)
        {
            var normal = _normals[i];
            var minBuffer = axisBuffer[i];
            var maxBuffer = axisBuffer[i + _normals.Length];

            RenderHelper.MapNormalVectorBuffer(minBuffer, minPoint - (UnitVector * Context.Application.ShortCurveTolerance), normal, axisLength);
            RenderHelper.MapNormalVectorBuffer(maxBuffer, maxPoint + (UnitVector * Context.Application.ShortCurveTolerance), -normal, axisLength);
        }

        _axisBuffers.AddRange(axisBuffer);
    }

    private void UpdateEffects()
    {
        foreach (var surfaceBuffer in _surfaceBuffers)
        {
            surfaceBuffer.EffectInstance ??= new EffectInstance(surfaceBuffer.FormatBits);
            surfaceBuffer.EffectInstance.SetColor(_surfaceColor);
            surfaceBuffer.EffectInstance.SetTransparency(_transparency);
        }

        foreach (var edgeBuffer in _edgeBuffers)
        {
            edgeBuffer.EffectInstance ??= new EffectInstance(edgeBuffer.FormatBits);
            edgeBuffer.EffectInstance.SetColor(_edgeColor);
        }

        foreach (var buffer in _axisBuffers)
        {
            buffer.EffectInstance ??= new EffectInstance(buffer.FormatBits);
            buffer.EffectInstance.SetColor(_axisColor);
        }
    }

    public void UpdateSurfaceColor(Color color)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _surfaceColor = color;
            hasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateEdgeColor(Color color)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _edgeColor = color;
            hasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateAxisColor(Color color)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _axisColor = color;
            hasEffectsUpdates = true;

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

    public void UpdateScale(double value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        _scale = value;

        lock (renderLock)
        {
            hasGeometryUpdates = true;
            hasEffectsUpdates = true;
            DisposeBuffers();

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

    public void UpdateEdgeVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _drawEdge = visible;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateAxisVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _drawAxis = visible;

            uiDocument.UpdateAllOpenViews();
        }
    }

    protected override void DisposeBuffers()
    {
        _surfaceBuffers.Clear(true);
        _edgeBuffers.Clear(true);
        _axisBuffers.Clear(true);
    }
}
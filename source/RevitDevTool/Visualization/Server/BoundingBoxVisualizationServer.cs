using System.Diagnostics;
using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Extensions ;
using RevitDevTool.Services ;
using RevitDevTool.Visualization.Contracts ;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
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

    private double _transparency = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.Transparency / 100;
    private double _scale = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.Scale / 100;
    private bool _drawSurface = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.ShowSurface;
    private bool _drawEdge = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.ShowEdge;
    private bool _drawAxis = SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.ShowAxis;

    private Color _surfaceColor = new(
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.SurfaceColor.R,
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.SurfaceColor.G, 
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.SurfaceColor.B);
    private Color _edgeColor = new(
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.EdgeColor.R, 
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.EdgeColor.G, 
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.EdgeColor.B);
    private Color _axisColor = new(
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.AxisColor.R, 
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.AxisColor.G, 
        SettingsService.Instance.VisualizationConfig.BoundingBoxSettings.AxisColor.B);

    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawSurface && _transparency > 0;

    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view)
    {
        if (VisualizeGeometries.Count == 0) return null;

        var allTransformedPoints = new List<XYZ>();
        
        foreach (var box in VisualizeGeometries)
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

    // ReSharper disable once CognitiveComplexity
    protected override void RenderScene()
    {
        if (VisualizeGeometries.Count == 0) return;
        
        if (HasGeometryUpdates || _surfaceBuffers.Count == 0 || _edgeBuffers.Count == 0)
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

        if (_drawEdge && _edgeBuffers.Count != 0)
        {
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

        if (_drawAxis)
        {
            foreach (var buffer in _axisBuffers)
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

    private void MapGeometryBuffer()
    {
        _surfaceBuffers.Clear(true);
        _edgeBuffers.Clear(true);
        _axisBuffers.Clear(true);

        if (VisualizeGeometries.Count == 0) return;
        
        try
        {
            foreach (var geometry in VisualizeGeometries)
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
            Trace.TraceError( $"Error mapping geometry buffer in BoundingBoxVisualizationServer: {ex}" ) ;
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

            RenderHelper.MapNormalVectorBuffer(minBuffer, minPoint - UnitVector * Context.Application.ShortCurveTolerance, normal, axisLength);
            RenderHelper.MapNormalVectorBuffer(maxBuffer, maxPoint + UnitVector * Context.Application.ShortCurveTolerance, -normal, axisLength);
        }

        _axisBuffers.AddRange(axisBuffer);
    }

    public override void UpdateEffects()
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
    
    public override void UpdateSurfaceColor(Color color)
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
    
    public override void UpdateEdgeColor(Color color)
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

    public override void UpdateAxisColor(Color color)
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
    
    public override void UpdateScale(double value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        _scale = value;
        
        lock (RenderLock)
        {
            HasGeometryUpdates = true;
            HasEffectsUpdates = true;
            _axisBuffers.Clear();
            _surfaceBuffers.Clear();
            _edgeBuffers.Clear();

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

    public override void UpdateEdgeVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawEdge = visible;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public override void UpdateAxisVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
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
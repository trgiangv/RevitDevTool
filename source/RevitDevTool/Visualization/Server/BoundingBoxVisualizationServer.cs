using System.Diagnostics;
using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using RevitDevTool.Visualization.Server.Contracts;
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

    private readonly double _transparency = BoundingBoxVisualizationSettings.Transparency;
    private readonly bool _drawSurface = BoundingBoxVisualizationSettings.ShowSurface;
    private readonly bool _drawEdge = BoundingBoxVisualizationSettings.ShowEdge;
    private readonly bool _drawAxis = BoundingBoxVisualizationSettings.ShowAxis;
    
    private readonly Color _surfaceColor = new(
        BoundingBoxVisualizationSettings.SurfaceColor.R,
        BoundingBoxVisualizationSettings.SurfaceColor.G, 
        BoundingBoxVisualizationSettings.SurfaceColor.B);
    private readonly Color _edgeColor = new(
        BoundingBoxVisualizationSettings.EdgeColor.R, 
        BoundingBoxVisualizationSettings.EdgeColor.G, 
        BoundingBoxVisualizationSettings.EdgeColor.B);
    private readonly Color _axisColor = new(
        BoundingBoxVisualizationSettings.AxisColor.R, 
        BoundingBoxVisualizationSettings.AxisColor.G, 
        BoundingBoxVisualizationSettings.AxisColor.B);

    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawSurface && _transparency > 0;

    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view)
    {
        if (VisualizeGeometries.Count == 0) return null;

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
                    if ((isTransparentPass && _transparency > 0) || (!isTransparentPass && _transparency == 0))
                    {
                        foreach (var surfaceBuffer in _surfaceBuffers.Where(b=>b.IsValid()))
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
                    foreach (var edgeBuffer in _edgeBuffers.Where(b=>b.IsValid()))
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
            catch (Exception exception)
            {
                Trace.TraceError($"Error in BoundingBoxVisualizationServer: {exception}");
            }
        }
    }

    private void MapGeometryBuffer()
    {
        if (VisualizeGeometries.Count == 0) return;
        
        try
        {
            foreach (var geometry in VisualizeGeometries)
            {
                var surfaceBuffer = new RenderingBufferStorage();
                RenderHelper.MapBoundingBoxSurfaceBuffer(surfaceBuffer, geometry);
                _surfaceBuffers.Add(surfaceBuffer);

                var edgeBuffer = new RenderingBufferStorage();
                RenderHelper.MapBoundingBoxEdgeBuffer(edgeBuffer, geometry);
                _edgeBuffers.Add(edgeBuffer);
            }

            MapAxisBuffers();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error mapping geometry buffer in BoundingBoxVisualizationServer: {ex}");
        }
    }

    private void MapAxisBuffers()
    {
        //_axisBuffers.Clear();
        
        foreach (var box in VisualizeGeometries)
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

                _axisBuffers.AddRange(axisBuffer);
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

    protected override void DisposeBuffers()
    {
        _surfaceBuffers.Clear();
        _edgeBuffers.Clear();
        _axisBuffers.Clear();
    }
}
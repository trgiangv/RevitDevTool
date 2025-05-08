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
    private readonly RenderingBufferStorage _surfaceBuffer = new();
    private readonly RenderingBufferStorage _edgeBuffer = new();
    private readonly List<RenderingBufferStorage> _axisBuffers = [];
    private readonly XYZ[] _normals =
    [
        XYZ.BasisX,
        XYZ.BasisY,
        XYZ.BasisZ
    ];

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

                if (_drawEdge && _edgeBuffer.IsValid())
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
            RenderHelper.MapBoundingBoxSurfaceBuffer(_surfaceBuffer, VisualizeGeometries);
            RenderHelper.MapBoundingBoxEdgeBuffer(_edgeBuffer, VisualizeGeometries);
            
            MapAxisBuffersForAllBoxes();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error mapping geometry buffer in BoundingBoxVisualizationServer: {ex}");
        }
    }

    private void MapAxisBuffersForAllBoxes()
    {
        _axisBuffers.Clear();
        
        foreach (var box in VisualizeGeometries)
        {
            var unitVector = new XYZ(1, 1, 1);
            var minPoint = box.Transform.OfPoint(box.Min);
            var maxPoint = box.Transform.OfPoint(box.Max);
            var axisLength = RenderGeometryHelper.InterpolateAxisLengthByPoints(minPoint, maxPoint);
            
            foreach (var normal in _normals)
            {
                var minBuffer = new RenderingBufferStorage();
                var maxBuffer = new RenderingBufferStorage();
                
                RenderHelper.MapNormalVectorBuffer(minBuffer, minPoint - unitVector * Context.Application.ShortCurveTolerance, normal, axisLength);
                RenderHelper.MapNormalVectorBuffer(maxBuffer, maxPoint + unitVector * Context.Application.ShortCurveTolerance, -normal, axisLength);
                
                _axisBuffers.Add(minBuffer);
                _axisBuffers.Add(maxBuffer);
            }
        }
    }

    private void UpdateEffects()
    {
        if (_surfaceBuffer.FormatBits == 0)
            _surfaceBuffer.FormatBits = VertexFormatBits.PositionNormal;
            
        _surfaceBuffer.EffectInstance ??= new EffectInstance(_surfaceBuffer.FormatBits);
        _surfaceBuffer.EffectInstance.SetColor(_surfaceColor);
        _surfaceBuffer.EffectInstance.SetTransparency(_transparency);

        if (_edgeBuffer.FormatBits == 0)
            _edgeBuffer.FormatBits = VertexFormatBits.Position;
            
        _edgeBuffer.EffectInstance ??= new EffectInstance(_edgeBuffer.FormatBits);
        _edgeBuffer.EffectInstance.SetColor(_edgeColor);

        foreach (var buffer in _axisBuffers)
        {
            if (buffer.FormatBits == 0)
                buffer.FormatBits = VertexFormatBits.Position;
                
            buffer.EffectInstance ??= new EffectInstance(buffer.FormatBits);
            buffer.EffectInstance.SetColor(_axisColor);
        }
    }

    protected override void DisposeBuffers()
    {
        _surfaceBuffer.Dispose();
        _edgeBuffer.Dispose();
        
        foreach (var buffer in _axisBuffers)
        {
            buffer.Dispose();
        }
        _axisBuffers.Clear();
    }
}
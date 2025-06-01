using System.Diagnostics;
using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using RevitDevTool.Visualization.Server.Contracts;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class XyzVisualizationServer : VisualizationServer<XYZ>
{
    private readonly Guid _serverId = new("A670E0BB-8B55-47CB-905C-7D94F0C8DF07");
    public override Guid GetServerId() => _serverId;
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

    private readonly double _transparency = XyzVisualizationSettings.Transparency;
    private readonly double _axisLength = XyzVisualizationSettings.AxisLength;

    private readonly Color _xColor = new(
        XyzVisualizationSettings.XColor.R,
        XyzVisualizationSettings.XColor.G,
        XyzVisualizationSettings.XColor.B);
    private readonly Color _yColor = new(
        XyzVisualizationSettings.YColor.R,
        XyzVisualizationSettings.YColor.G,
        XyzVisualizationSettings.YColor.B);
    private readonly Color _zColor = new(
        XyzVisualizationSettings.ZColor.R,
        XyzVisualizationSettings.ZColor.G,
        XyzVisualizationSettings.ZColor.B);

    private readonly bool _drawPlane = XyzVisualizationSettings.ShowPlane;
    private readonly bool _drawXAxis = XyzVisualizationSettings.ShowXAxis;
    private readonly bool _drawYAxis = XyzVisualizationSettings.ShowYAxis;
    private readonly bool _drawZAxis = XyzVisualizationSettings.ShowZAxis;
    
    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawPlane && _transparency > 0;

    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view)
    {
        if (VisualizeGeometries.Count == 0) return null;
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
                if (VisualizeGeometries.Count == 0) return;
                
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
                Trace.TraceError($"Error in XyzVisualizationServer: {exception.Message}");
            }
        }
    }

    private void RenderPlaneBuffer(RenderingBufferStorage buffer)
    {
        if (!_drawPlane || !buffer.IsValid()) return;

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

    private static void RenderAxisBuffer(RenderingBufferStorage buffer)
    {
        if (!buffer.IsValid()) return;
        
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
        if (VisualizeGeometries.Count == 0) return;
        
        try
        {
            MapNormalBuffer();
            MapPlaneBuffer();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error updating geometry buffer in XyzVisualizationServer: {ex}");
        }
    }

    private void MapNormalBuffer()
    {
        if (VisualizeGeometries.Count == 0) return;
        
        var normalExtendLength = _axisLength > 1 ? 0.8 : _axisLength * 0.8;
        for (var i = 0; i < _normals.Length; i++)
        {
            var normal = _normals[i];
            var buffer = _axisBuffers[i];
            RenderHelper.MapNormalVectorBufferForMultiplePoints(buffer, VisualizeGeometries, normal, 2 * (_axisLength + normalExtendLength));
        }
    }

    private void MapPlaneBuffer()
    {
        if (VisualizeGeometries.Count == 0) return;
        
        for (var i = 0; i < _normals.Length; i++)
        {
            var normal = _normals[i];
            var buffer = _planeBuffers[i];
            RenderHelper.MapSideBufferForMultiplePoints(buffer, VisualizeGeometries, normal, _axisLength);
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

    protected override void DisposeBuffers()
    {
        foreach (var buffer in _planeBuffers)
        {
            buffer.Dispose();
        }
        
        foreach (var buffer in _axisBuffers)
        {
            buffer.Dispose();
        }
    }
}
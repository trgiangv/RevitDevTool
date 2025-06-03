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

    private readonly List<RenderingBufferStorage[]> _planeBufferArrays = [];
    private readonly List<RenderingBufferStorage[]> _axisBufferArrays = [];

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
                    var renderAxisBuffers = _axisBufferArrays.Select(axisBufferArray => axisBufferArray[0]).ToArray();
                    var renderPlaneBuffers = _planeBufferArrays.Select(planeBufferArray => planeBufferArray[0]).ToArray();
                    RenderAxisBuffer(renderAxisBuffers);
                    RenderPlaneBuffer(renderPlaneBuffers);
                }

                if (_drawYAxis)
                {
                    var renderAxisBuffers = _axisBufferArrays.Select(axisBufferArray => axisBufferArray[1]).ToArray();
                    var renderPlaneBuffers = _planeBufferArrays.Select(planeBufferArray => planeBufferArray[1]).ToArray();
                    RenderAxisBuffer(renderAxisBuffers);
                    RenderPlaneBuffer(renderPlaneBuffers);
                }

                if (_drawZAxis)
                {
                    var renderAxisBuffers = _axisBufferArrays.Select(axisBufferArray => axisBufferArray[2]).ToArray();
                    var renderPlaneBuffers = _planeBufferArrays.Select(planeBufferArray => planeBufferArray[2]).ToArray();
                    RenderAxisBuffer(renderAxisBuffers);
                    RenderPlaneBuffer(renderPlaneBuffers);
                }
            }
            catch (Exception exception)
            {
                Trace.TraceError($"Error in XyzVisualizationServer: {exception.Message}");
            }
        }
    }

    private void RenderPlaneBuffer(RenderingBufferStorage[] bufferArray)
    {
        if (!_drawPlane) return;

        var isTransparentPass = DrawContext.IsTransparentPass();
        if ((!isTransparentPass || !(_transparency > 0)) && (isTransparentPass || _transparency != 0)) return;

        foreach (var buffer in bufferArray)
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

    private static void RenderAxisBuffer(RenderingBufferStorage[] bufferArray)
    {
        if (bufferArray.Length == 0) return;

        foreach (var buffer in bufferArray)
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

    private void UpdateGeometryBuffer()
    {
        DisposeBuffers();

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
        foreach (var visualizeGeometry in VisualizeGeometries)
        {
            var axisBuffers = Enumerable.Range(0, 3)
                .Select(_ => new RenderingBufferStorage())
                .ToArray();

            for (var i = 0; i < _normals.Length; i++)
            {
                var normal = _normals[i];
                var buffer = axisBuffers[i];
                RenderHelper.MapNormalVectorBuffer(buffer, visualizeGeometry - normal * (_axisLength + normalExtendLength), normal, 2 * (_axisLength + normalExtendLength));
            }

            _axisBufferArrays.Add(axisBuffers);
        }
    }

    private void MapPlaneBuffer()
    {
        if (VisualizeGeometries.Count == 0) return;

        foreach (var visualizeGeometry in VisualizeGeometries)
        {
            var planeBuffers = Enumerable.Range(0, 3)
                .Select(_ => new RenderingBufferStorage())
                .ToArray();

            for (var i = 0; i < _normals.Length; i++)
            {
                var normal = _normals[i];
                var buffer = planeBuffers[i];
                RenderHelper.MapSideBuffer(buffer, visualizeGeometry - normal * _axisLength, visualizeGeometry + normal * _axisLength);
            }

            _planeBufferArrays.Add(planeBuffers);
        }
    }

    private void UpdateEffects()
    {
        foreach (var bufferArray in _planeBufferArrays)
        {
            foreach (var buffer in bufferArray)
            {
                buffer.EffectInstance ??= new EffectInstance(buffer.FormatBits);
                buffer.EffectInstance.SetTransparency(_transparency);
            }

            bufferArray[0].EffectInstance!.SetColor(_xColor);
            bufferArray[1].EffectInstance!.SetColor(_yColor);
            bufferArray[2].EffectInstance!.SetColor(_zColor);
        }

        foreach (var bufferArray in _axisBufferArrays)
        {
            foreach (var buffer in bufferArray)
            {
                buffer.EffectInstance ??= new EffectInstance(buffer.FormatBits);
            }

            bufferArray[0].EffectInstance!.SetColor(_xColor);
            bufferArray[1].EffectInstance!.SetColor(_yColor);
            bufferArray[2].EffectInstance!.SetColor(_zColor);
        }
    }

    protected override void DisposeBuffers()
    {
        _axisBufferArrays.Clear(true);
        _planeBufferArrays.Clear(true);
    }
}
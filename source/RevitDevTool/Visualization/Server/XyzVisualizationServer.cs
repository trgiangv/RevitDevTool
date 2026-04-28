using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Settings;
using DevTools.Utilities;
using RevitDevTool.Visualization.Contracts;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using System.Diagnostics;
using RevitDevTool.Core;
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

    private double _transparency;
    private double _axisLength;
    private Color _xColor;
    private Color _yColor;
    private Color _zColor;
    private bool _drawPlane;
    private bool _drawXAxis;
    private bool _drawYAxis;
    private bool _drawZAxis;

    public XyzVisualizationServer(IRevitSettingsService settingsService)
    {
        var settings = settingsService.VisualizationConfig.XyzSettings;
        _transparency = settings.Transparency / 100;
        _axisLength = settings.AxisLength / 12;
        _xColor = new Color(settings.XColor.R, settings.XColor.G, settings.XColor.B);
        _yColor = new Color(settings.YColor.R, settings.YColor.G, settings.YColor.B);
        _zColor = new Color(settings.ZColor.R, settings.ZColor.G, settings.ZColor.B);
        _drawPlane = settings.ShowPlane;
        _drawXAxis = settings.ShowXAxis;
        _drawYAxis = settings.ShowYAxis;
        _drawZAxis = settings.ShowZAxis;
    }

    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawPlane && _transparency > 0;

    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view)
    {
        if (VisualizeGeometries.Count == 0) return null;
        var minPoint = new XYZ(VisualizeGeometries.Min(p => p.X) - _axisLength, VisualizeGeometries.Min(p => p.Y) - _axisLength, VisualizeGeometries.Min(p => p.Z) - _axisLength);
        var maxPoint = new XYZ(VisualizeGeometries.Max(p => p.X) + _axisLength, VisualizeGeometries.Max(p => p.Y) + _axisLength, VisualizeGeometries.Max(p => p.Z) + _axisLength);

        return new Outline(minPoint, maxPoint);
    }

    protected override void RenderScene()
    {
        if (VisualizeGeometries.Count == 0) return;

        if (HasGeometryUpdates)
        {
            MapGeometryBuffer();
            HasGeometryUpdates = false;
        }

        if (HasEffectsUpdates)
        {
            UpdateEffects();
            HasEffectsUpdates = false;
        }

        RenderAxisByIndex(0, _drawXAxis);
        RenderAxisByIndex(1, _drawYAxis);
        RenderAxisByIndex(2, _drawZAxis);
    }

    private void RenderAxisByIndex(int index, bool shouldDraw)
    {
        if (!shouldDraw) return;

        var renderAxisBuffers = _axisBufferArrays.Select(axisBufferArray => axisBufferArray[index]).ToArray();
        var renderPlaneBuffers = _planeBufferArrays.Select(planeBufferArray => planeBufferArray[index]).ToArray();
        RenderAxisBuffer(renderAxisBuffers);
        RenderPlaneBuffer(renderPlaneBuffers);
    }

    private void RenderPlaneBuffer(RenderingBufferStorage[] bufferArray)
    {
        if (!_drawPlane) return;
        if (!ShouldRenderTransparentPass(_transparency)) return;

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

    private void MapGeometryBuffer()
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
                RenderHelper.MapNormalVectorBuffer(buffer, visualizeGeometry - (normal * (_axisLength + normalExtendLength)), normal, 2 * (_axisLength + normalExtendLength));
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
                RenderHelper.MapSideBuffer(buffer, visualizeGeometry - (normal * _axisLength), visualizeGeometry + (normal * _axisLength));
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

    public void UpdateXColor(Color value)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
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
        var uiDocument = RevitContext.ActiveUiDocument;
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
        var uiDocument = RevitContext.ActiveUiDocument;
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
        var uiDocument = RevitContext.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _axisLength = value;
            HasGeometryUpdates = true;
            HasEffectsUpdates = true;
            DisposeBuffers();

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateTransparency(double value)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
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
        var uiDocument = RevitContext.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawPlane = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateXAxisVisibility(bool visible)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawXAxis = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateYAxisVisibility(bool visible)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawYAxis = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateZAxisVisibility(bool visible)
    {
        var uiDocument = RevitContext.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (RenderLock)
        {
            _drawZAxis = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    protected override void DisposeBuffers()
    {
        _axisBufferArrays.Clear(true);
        _planeBufferArrays.Clear(true);
    }
}
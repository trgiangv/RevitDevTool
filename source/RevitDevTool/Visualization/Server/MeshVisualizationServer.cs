using Autodesk.Revit.DB.DirectContext3D;
using RevitDevTool.Settings;
using RevitDevTool.Utils;
using RevitDevTool.Visualization.Contracts;
using RevitDevTool.Visualization.Helpers;
using RevitDevTool.Visualization.Render;
using System.Diagnostics;
using Color = Autodesk.Revit.DB.Color;

namespace RevitDevTool.Visualization.Server;

public sealed class MeshVisualizationServer : VisualizationServer<Mesh>
{
    private readonly Guid _serverId = new("FD6F0E82-26D9-485B-A6D8-5FA65B85442F");
    public override Guid GetServerId() => _serverId;
    private readonly List<RenderingBufferStorage[]> _normalBuffers = [];
    private readonly List<RenderingBufferStorage> _surfaceBuffers = [];
    private readonly List<RenderingBufferStorage> _meshGridBuffers = [];

    private double _extrusion;
    private double _transparency;
    private bool _drawMeshGrid;
    private bool _drawNormalVector;
    private bool _drawSurface;
    private Color _meshColor;
    private Color _normalColor;
    private Color _surfaceColor;

    public MeshVisualizationServer(ISettingsService settingsService)
    {
        var settings = settingsService.VisualizationConfig.MeshSettings;
        _extrusion = settings.Extrusion;
        _transparency = settings.Transparency;
        _drawMeshGrid = settings.ShowMeshGrid;
        _drawNormalVector = settings.ShowNormalVector;
        _drawSurface = settings.ShowSurface;
        _meshColor = new Color(settings.MeshColor.R, settings.MeshColor.G, settings.MeshColor.B);
        _normalColor = new Color(settings.NormalVectorColor.R, settings.NormalVectorColor.G, settings.NormalVectorColor.B);
        _surfaceColor = new Color(settings.SurfaceColor.R, settings.SurfaceColor.G, settings.SurfaceColor.B);
    }

    public override bool UseInTransparentPass(Autodesk.Revit.DB.View view) => _drawSurface && _transparency > 0;

    public override Outline? GetBoundingBox(Autodesk.Revit.DB.View view)
    {
        return null;
    }

    protected override void RenderScene()
    {
        if (visualizeGeometries.Count == 0) return;

        if (hasGeometryUpdates || _surfaceBuffers.Count == 0 || _meshGridBuffers.Count == 0)
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
        RenderMeshGridBuffers();
        RenderNormalBuffers();
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

    private void RenderMeshGridBuffers()
    {
        if (!_drawMeshGrid || _meshGridBuffers.Count == 0) return;

        foreach (var meshGridBuffer in _meshGridBuffers)
        {
            DrawContext.FlushBuffer(meshGridBuffer.VertexBuffer,
                meshGridBuffer.VertexBufferCount,
                meshGridBuffer.IndexBuffer,
                meshGridBuffer.IndexBufferCount,
                meshGridBuffer.VertexFormat,
                meshGridBuffer.EffectInstance, PrimitiveType.LineList, 0,
                meshGridBuffer.PrimitiveCount);
        }
    }

    private void RenderNormalBuffers()
    {
        if (!_drawNormalVector || _normalBuffers.Count == 0) return;

        foreach (var buffer in _normalBuffers.SelectMany(buffers => buffers))
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
                var surfaceBuffer = new RenderingBufferStorage();
                RenderHelper.MapSurfaceBuffer(surfaceBuffer, visualizeGeometry, _extrusion);
                _surfaceBuffers.Add(surfaceBuffer);

                var meshGridBuffer = new RenderingBufferStorage();
                RenderHelper.MapMeshGridBuffer(meshGridBuffer, visualizeGeometry, _extrusion);
                _meshGridBuffers.Add(meshGridBuffer);
            }

            MapNormalsBuffer();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Error mapping geometry buffer in MeshVisualizationServer: {ex}");
        }
    }

    private void MapNormalsBuffer()
    {
        _normalBuffers.Clear(true);

        foreach (var mesh in visualizeGeometries)
        {
            var area = RenderGeometryHelper.ComputeMeshSurfaceArea(mesh);
            var offset = RenderGeometryHelper.InterpolateOffsetByArea(area);
            var normalLength = RenderGeometryHelper.InterpolateAxisLengthByArea(area);

            var normals = Enumerable.Range(0, mesh.Vertices.Count)
                .Select(_ => new RenderingBufferStorage())
                .ToArray();

            for (var i = 0; i < mesh.Vertices.Count; i++)
            {
                var vertex = mesh.Vertices[i];
                var buffer = normals[i];
                var normal = RenderGeometryHelper.GetMeshVertexNormal(mesh, i, mesh.DistributionOfNormals);
                RenderHelper.MapNormalVectorBuffer(buffer, vertex + (normal * (offset + _extrusion)), normal, normalLength);
            }

            _normalBuffers.Add(normals);
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

        foreach (var meshGridBuffer in _meshGridBuffers)
        {
            meshGridBuffer.EffectInstance ??= new EffectInstance(meshGridBuffer.FormatBits);
            meshGridBuffer.EffectInstance.SetColor(_meshColor);
        }

        foreach (var normalBuffer in _normalBuffers)
        {
            foreach (var buffer in normalBuffer)
            {
                buffer.EffectInstance ??= new EffectInstance(buffer.FormatBits);
                buffer.EffectInstance.SetColor(_normalColor);
            }
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

    public void UpdateMeshGridColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _meshColor = value;
            hasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateNormalVectorColor(Color value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _normalColor = value;
            hasEffectsUpdates = true;

            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateExtrusion(double value)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _extrusion = value;
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

    public void UpdateMeshGridVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _drawMeshGrid = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    public void UpdateNormalVectorVisibility(bool visible)
    {
        var uiDocument = Context.ActiveUiDocument;
        if (uiDocument is null) return;

        lock (renderLock)
        {
            _drawNormalVector = visible;
            uiDocument.UpdateAllOpenViews();
        }
    }

    protected override void DisposeBuffers()
    {
        _surfaceBuffers.Clear(true);
        _meshGridBuffers.Clear(true);
        _normalBuffers.Clear(true);
    }
}
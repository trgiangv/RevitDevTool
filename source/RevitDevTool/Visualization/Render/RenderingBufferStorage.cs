using Autodesk.Revit.DB.DirectContext3D;

namespace RevitDevTool.Visualization.Render;

#nullable disable
public sealed class RenderingBufferStorage : IDisposable
{
    public VertexFormatBits FormatBits { get; set; }
    public int PrimitiveCount { get; set; }
    public int VertexBufferCount { get; set; }
    public int IndexBufferCount { get; set; }
    public VertexBuffer VertexBuffer { get; set; }
    public IndexBuffer IndexBuffer { get; set; }
    public VertexFormat VertexFormat { get; set; }
    public EffectInstance EffectInstance { get; set; }
    
    public bool IsValid()
    {
        try
        {
            if (VertexBuffer == null || !VertexBuffer.IsValid()) return false;
            if (IndexBuffer == null || !IndexBuffer.IsValid()) return false;
            if (VertexFormat == null || !VertexFormat.IsValid()) return false;
            if (EffectInstance == null || !EffectInstance.IsValid()) return false;
            if (PrimitiveCount <= 0 || VertexBufferCount <= 0 || IndexBufferCount <= 0) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public void Dispose()
    {
        try
        {
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();
            VertexFormat?.Dispose();
            EffectInstance?.Dispose();
        }
        finally
        {
            VertexBuffer = null;
            IndexBuffer = null;
            VertexFormat = null;
            EffectInstance = null;
        }
    }
}
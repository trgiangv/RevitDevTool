using System.Text;
using RevitDevTool.Scintilla.Core;
namespace RevitDevTool.Scintilla.Formatting;

internal sealed class RenderTokenWriter(IList<RenderSegment> segments)
{
    public void Add(
        string text,
        LogSemanticStyle style,
        ILogTokenPayload? payload = null,
        bool isLink = false,
        string? customStyleKey = null)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var bytes = Encoding.UTF8.GetByteCount(text);
        if (bytes > 0)
            segments.Add(new RenderSegment(bytes, style, payload, isLink, customStyleKey));
    }

    public void Add(
        string source,
        int start,
        int length,
        LogSemanticStyle style,
        ILogTokenPayload? payload = null,
        bool isLink = false,
        string? customStyleKey = null)
    {
        if (length <= 0 || start < 0 || start + length > source.Length)
            return;

#if NET8_0_OR_GREATER
        var bytes = Encoding.UTF8.GetByteCount(source.AsSpan(start, length));
#else
        var bytes = Encoding.UTF8.GetByteCount(source.Substring(start, length));
#endif
        if (bytes > 0)
            segments.Add(new RenderSegment(bytes, style, payload, isLink, customStyleKey));
    }
}

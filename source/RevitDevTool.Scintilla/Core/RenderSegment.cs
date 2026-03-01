using RevitDevTool.Scintilla.Formatting;
namespace RevitDevTool.Scintilla.Core;

public readonly struct RenderSegment
{
    public RenderSegment(
        int utf8Length,
        LogSemanticStyle semanticStyle,
        ILogTokenPayload? tokenPayload = null,
        bool isLink = false,
        string? customStyleKey = null)
    {
        Utf8Length = utf8Length;
        SemanticStyle = semanticStyle;
        TokenPayload = tokenPayload;
        IsLink = isLink;
        CustomStyleKey = customStyleKey;
    }

    public int Utf8Length { get; }
    public LogSemanticStyle SemanticStyle { get; }
    public ILogTokenPayload? TokenPayload { get; }
    public bool IsLink { get; }
    public string? CustomStyleKey { get; }
}

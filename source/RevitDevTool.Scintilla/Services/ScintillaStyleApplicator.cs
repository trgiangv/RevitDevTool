using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Formatting;
using RevitDevTool.Scintilla.Render;
namespace RevitDevTool.Scintilla.Services;

internal sealed class ScintillaStyleApplicator
{
    private readonly ScintillaNET.Scintilla _scintilla;
    private readonly ScintillaLogViewerOptions _options;
    private readonly ILogStyleRegistry _styleRegistry;
    private readonly List<TokenRange> _tokenRanges;
    private readonly Dictionary<int, int> _linkStyleByBaseStyleId;

    public ScintillaStyleApplicator(
        ScintillaNET.Scintilla scintilla,
        ScintillaLogViewerOptions options,
        ILogStyleRegistry styleRegistry,
        List<TokenRange> tokenRanges,
        Dictionary<int, int> linkStyleByBaseStyleId)
    {
        _scintilla = scintilla;
        _options = options;
        _styleRegistry = styleRegistry;
        _tokenRanges = tokenRanges;
        _linkStyleByBaseStyleId = linkStyleByBaseStyleId;
    }

    public void AppendStyleRangesFromSegments(
        IReadOnlyList<RenderSegment> segments,
        Microsoft.Extensions.Logging.LogLevel level,
        int expectedLineBytes,
        List<PendingStyleRange> styleRanges)
    {
        if (segments.Count == 0)
        {
            styleRanges.Add(new PendingStyleRange(expectedLineBytes, _styleRegistry.GetStyleId(level), null, false));
            return;
        }

        var total = 0;
        for (var i = 0; i < segments.Count; i++)
            total += segments[i].Utf8Length;

        if (total != expectedLineBytes)
        {
            styleRanges.Add(new PendingStyleRange(expectedLineBytes, _styleRegistry.GetStyleId(level), null, false));
            return;
        }

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment.Utf8Length <= 0)
                continue;

            var styleId = ResolveSegmentStyleId(segment);
            styleRanges.Add(new PendingStyleRange(segment.Utf8Length, styleId, segment.TokenPayload, segment.IsLink));
        }
    }

    public void ApplyStylesAndTokenRanges(int startPos, int writtenLength, bool autoScroll, IReadOnlyList<PendingStyleRange> styleRanges)
    {
        if (writtenLength <= 0)
            return;

        _scintilla.StartStyling(startPos);
        _scintilla.IndicatorCurrent = _styleRegistry.GetTokenIndicatorStyleId();
        _scintilla.IndicatorClearRange(startPos, writtenLength);

        var cursor = startPos;
        for (var i = 0; i < styleRanges.Count; i++)
        {
            var range = styleRanges[i];
            var styleId = range.IsLink ? ResolveLinkStyleId(range.StyleId) : range.StyleId;
            _scintilla.SetStyling(range.Length, styleId);

            if (range.TokenPayload is not null)
            {
                if (_options.EnableTokenLinks && range.IsLink)
                    _tokenRanges.Add(new TokenRange(cursor, range.Length, range.TokenPayload));
            }

            cursor += range.Length;
        }

        if (autoScroll)
        {
            _scintilla.GotoPosition(_scintilla.TextLength);
            _scintilla.ScrollCaret();
        }
    }

    public void RefreshLinkStylesFromBaseStyles()
    {
        foreach (var pair in _linkStyleByBaseStyleId)
            CopyBaseStyleToLinkStyle(pair.Key, pair.Value);
    }

    private int ResolveLinkStyleId(int baseStyleId)
    {
        if (_linkStyleByBaseStyleId.TryGetValue(baseStyleId, out var resolved))
            return resolved;

        var candidateStyleId = baseStyleId + _options.LinkStyleOffset;
        if (candidateStyleId <= 0 || candidateStyleId >= 255)
            candidateStyleId = _styleRegistry.GetLinkHotspotStyleId();

        if (candidateStyleId != _styleRegistry.GetLinkHotspotStyleId())
            CopyBaseStyleToLinkStyle(baseStyleId, candidateStyleId);

        _linkStyleByBaseStyleId[baseStyleId] = candidateStyleId;
        return candidateStyleId;
    }

    private void CopyBaseStyleToLinkStyle(int baseStyleId, int linkStyleId)
    {
        var source = _scintilla.Styles[baseStyleId];
        var target = _scintilla.Styles[linkStyleId];
        target.ForeColor = source.ForeColor;
        target.BackColor = source.BackColor;
        target.Bold = source.Bold;
        target.Hotspot = true;
        target.Underline = true;
    }

    private int ResolveSegmentStyleId(in RenderSegment segment)
    {
        var styleKey = segment.CustomStyleKey;
        if (styleKey is not null &&
            styleKey.Length > 0 &&
            _styleRegistry.TryGetStyleId(styleKey, out var customStyleId))
        {
            return customStyleId;
        }

        return _styleRegistry.GetStyleId(segment.SemanticStyle);
    }
}

internal readonly struct PendingStyleRange
{
    public PendingStyleRange(int length, int styleId, ILogTokenPayload? tokenPayload, bool isLink)
    {
        Length = length;
        StyleId = styleId;
        TokenPayload = tokenPayload;
        IsLink = isLink;
    }

    public int Length { get; }
    public int StyleId { get; }
    public ILogTokenPayload? TokenPayload { get; }
    public bool IsLink { get; }
}

internal readonly struct TokenRange
{
    public TokenRange(int start, int length, ILogTokenPayload payload)
    {
        Start = start;
        Length = length;
        Payload = payload;
    }

    public int Start { get; }
    public int Length { get; }
    public ILogTokenPayload Payload { get; }
}

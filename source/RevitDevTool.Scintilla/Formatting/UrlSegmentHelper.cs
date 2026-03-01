using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Helpers;
namespace RevitDevTool.Scintilla.Formatting;

/// <summary>
/// Shared URL detection and segment generation used by both
/// <see cref="DisplayValueFormatter"/> and <see cref="JsonValueFormatter"/>.
/// </summary>
internal sealed class UrlSegmentHelper
{
    private readonly List<(int Start, int Length)> _matchBuffer = new(8);

    /// <summary>
    /// Scans <paramref name="text"/> for URLs. For each URL found, emits a link segment;
    /// non-URL gaps are forwarded to <paramref name="gapHandler"/>.
    /// </summary>
    public void AppendUrlAwareSegments(
        string text,
        int start,
        int length,
        LogSemanticStyle defaultStyle,
        RenderTokenWriter writer,
        Action<string, int, int, RenderTokenWriter>? gapHandler)
    {
        if (length <= 0)
            return;

        var slice = text.Substring(start, length);

        if (!UrlScanner.HasPotentialCandidate(slice) || UrlScanner.FindAll(slice, _matchBuffer) == 0)
        {
            if (gapHandler is not null)
                gapHandler(text, start, length, writer);
            else
                writer.Add(text, start, length, defaultStyle);
            return;
        }

        var cursor = 0;
        for (var i = 0; i < _matchBuffer.Count; i++)
        {
            var match = _matchBuffer[i];

            if (match.Start > cursor)
            {
                if (gapHandler is not null)
                    gapHandler(text, start + cursor, match.Start - cursor, writer);
                else
                    writer.Add(text, start + cursor, match.Start - cursor, defaultStyle);
            }

            EmitUrlOrFallback(slice, match.Start, match.Length, defaultStyle, writer);
            cursor = match.Start + match.Length;
        }

        if (cursor < slice.Length)
        {
            if (gapHandler is not null)
                gapHandler(text, start + cursor, slice.Length - cursor, writer);
            else
                writer.Add(text, start + cursor, slice.Length - cursor, defaultStyle);
        }
    }

    /// <summary>
    /// Scans <paramref name="content"/> (a standalone string, not a substring) for URLs.
    /// Emits link segments for URLs and styled segments for gaps.
    /// Used by <see cref="JsonValueFormatter"/> for JSON string content where the caller's
    /// style (e.g. <see cref="LogSemanticStyle.JsonString"/>) must be preserved for URLs —
    /// only the hotspot/clickable flag is set, not the color.
    /// </summary>
    public void AppendUrlAwareContent(
        string content,
        LogSemanticStyle defaultStyle,
        RenderTokenWriter writer)
    {
        if (string.IsNullOrEmpty(content))
            return;

        if (!UrlScanner.HasPotentialCandidate(content) || UrlScanner.FindAll(content, _matchBuffer) == 0)
        {
            writer.Add(content, defaultStyle);
            return;
        }

        var cursor = 0;
        for (var i = 0; i < _matchBuffer.Count; i++)
        {
            var match = _matchBuffer[i];

            if (match.Start > cursor)
                writer.Add(content, cursor, match.Start - cursor, defaultStyle);

            EmitUrlOrFallback(content, match.Start, match.Length, defaultStyle, writer, preserveStyle: true);
            cursor = match.Start + match.Length;
        }

        if (cursor < content.Length)
            writer.Add(content, cursor, content.Length - cursor, defaultStyle);
    }

    private static void EmitUrlOrFallback(
        string source,
        int start,
        int length,
        LogSemanticStyle fallbackStyle,
        RenderTokenWriter writer,
        bool preserveStyle = false)
    {
        var urlText = source.Substring(start, length);
        if (UrlScanner.TryNormalizeUri(urlText, out var targetUri))
        {
            // preserveStyle=true: keep caller's color (e.g. JsonString) but still mark as link.
            // preserveStyle=false: override with TokenLink color (plain-text context).
            var style = preserveStyle ? fallbackStyle : LogSemanticStyle.TokenLink;
            writer.Add(urlText, style, new UrlTokenPayload(urlText, targetUri), isLink: true);
        }
        else
        {
            writer.Add(urlText, fallbackStyle);
        }
    }

    private sealed record UrlTokenPayload(string DisplayText, string TargetUri) : ILogTokenPayload
    {
        public LogSemanticStyle SemanticStyle => LogSemanticStyle.TokenLink;
        public string? StyleKey => null;
        public bool IsLink => true;
    }
}

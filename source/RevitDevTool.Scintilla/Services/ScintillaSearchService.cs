using RevitDevTool.Scintilla.Search;
using RevitDevTool.Scintilla.Core;
using ScintillaNET;
namespace RevitDevTool.Scintilla.Services;

internal sealed class ScintillaSearchService
{
    private readonly ScintillaNET.Scintilla _scintilla;
    private const int SearchIndicatorId = 30;
    private const int SciPositionFromPoint = 2022;
    private string? _activePattern;
    private SearchFlags _activeFlags;
    private bool _hasActiveSearch;

    public ScintillaSearchService(ScintillaNET.Scintilla scintilla)
    {
        _scintilla = scintilla;
        ConfigureSearchIndicator(ScintillaTheme.Dark);
    }

    public LogSearchResult FindNext(string pattern, bool matchCase, bool useRegex)
    {
        SetActiveSearch(pattern, matchCase, useRegex);
        if (string.IsNullOrWhiteSpace(pattern))
            return LogSearchResult.NotFound;

        if (_scintilla.TextLength == 0)
            return LogSearchResult.NotFound;

        var start = Clamp(_scintilla.CurrentPosition, 0, _scintilla.TextLength);
        var flags = BuildSearchFlags(matchCase, useRegex);

        var found = SearchWithWrap(pattern, flags, start, _scintilla.TextLength);
        if (found.Found)
        {
            _scintilla.SetSelection(found.Position + found.Length, found.Position);
            _scintilla.ScrollCaret();
        }

        HighlightVisibleMatches(pattern, flags);
        if (!found.Found)
            return LogSearchResult.NotFound;

        return new LogSearchResult(true, found.Position, found.Length);
    }

    public LogSearchResult FindPrevious(string pattern, bool matchCase, bool useRegex)
    {
        SetActiveSearch(pattern, matchCase, useRegex);
        if (string.IsNullOrWhiteSpace(pattern))
            return LogSearchResult.NotFound;

        if (_scintilla.TextLength == 0)
            return LogSearchResult.NotFound;

        var flags = BuildSearchFlags(matchCase, useRegex);
        var start = Clamp(_scintilla.CurrentPosition - 1, 0, _scintilla.TextLength);
        var found = SearchBackwardWithWrap(pattern, flags, start, _scintilla.TextLength);
        if (found.Found)
        {
            // For backward search keep caret at match start, otherwise the next backward
            // query can land on the same match repeatedly.
            _scintilla.SetSelection(found.Position, found.Position + found.Length);
            _scintilla.ScrollCaret();
        }

        HighlightVisibleMatches(pattern, flags);
        if (!found.Found)
            return LogSearchResult.NotFound;

        return new LogSearchResult(true, found.Position, found.Length);
    }

    public LogSearchResult HighlightSearch(string pattern, bool matchCase, bool useRegex)
    {
        SetActiveSearch(pattern, matchCase, useRegex);
        RefreshHighlightIfActive();
        return LogSearchResult.NotFound;
    }

    public bool HasActiveSearch => _hasActiveSearch;

    public void RefreshHighlightIfActive()
    {
        var pattern = _activePattern;
        if (!_hasActiveSearch || string.IsNullOrWhiteSpace(pattern))
        {
            ClearSearchHighlight();
            return;
        }

        HighlightVisibleMatches(pattern!, _activeFlags);
    }

    public void RefreshHighlightForAppendedRange(int appendedStart, int appendedLength)
    {
        var pattern = _activePattern;
        if (!_hasActiveSearch || string.IsNullOrWhiteSpace(pattern))
            return;

        if (appendedLength <= 0 || _scintilla.TextLength == 0)
            return;

        if (!IntersectsVisibleRange(appendedStart, appendedLength))
            return;

        RefreshHighlightIfActive();
    }

    public void ClearSearchHighlight()
    {
        _activePattern = null;
        _activeFlags = SearchFlags.None;
        _hasActiveSearch = false;
        ClearAllHighlight();
    }

    private (bool Found, int Position, int Length) SearchWithWrap(string pattern, SearchFlags flags, int start, int end)
    {
        _scintilla.SearchFlags = flags;

        var found = SearchInRange(pattern, start, end);
        if (found.Found)
            return found;

        if (start <= 0)
            return (false, -1, 0);

        return SearchInRange(pattern, 0, start);
    }

    private (bool Found, int Position, int Length) SearchInRange(string pattern, int start, int end)
    {
        _scintilla.TargetStart = start;
        _scintilla.TargetEnd = end;

        var foundPosition = _scintilla.SearchInTarget(pattern);
        if (foundPosition < 0)
            return (false, -1, 0);

        var matchLength = _scintilla.TargetEnd - _scintilla.TargetStart;
        return (true, foundPosition, Math.Max(0, matchLength));
    }

    private (bool Found, int Position, int Length) SearchBackwardWithWrap(string pattern, SearchFlags flags, int start, int end)
    {
        _scintilla.SearchFlags = flags;

        var found = SearchInRangeBackward(pattern, start, 0);
        if (found.Found)
            return found;

        if (start >= end)
            return (false, -1, 0);

        return SearchInRangeBackward(pattern, end, start + 1);
    }

    private (bool Found, int Position, int Length) SearchInRangeBackward(string pattern, int start, int end)
    {
        _scintilla.TargetStart = start;
        _scintilla.TargetEnd = end;

        var foundPosition = _scintilla.SearchInTarget(pattern);
        if (foundPosition < 0)
            return (false, -1, 0);

        var matchLength = _scintilla.TargetEnd - _scintilla.TargetStart;
        return (true, foundPosition, Math.Max(0, matchLength));
    }

    private bool IntersectsVisibleRange(int start, int length)
    {
        if (length <= 0 || _scintilla.TextLength == 0 || _scintilla.Lines.Count == 0)
            return false;

        var (visibleStart, visibleEnd) = GetVisibleRange();
        if (visibleEnd <= visibleStart)
            return false;

        var appendStart = Clamp(start, 0, _scintilla.TextLength);
        var appendEnd = Clamp(start + length, 0, _scintilla.TextLength);
        return appendEnd > visibleStart && appendStart < visibleEnd;
    }

    private void HighlightVisibleMatches(string pattern, SearchFlags flags)
    {
        ClearAllHighlight();

        if (string.IsNullOrWhiteSpace(pattern) || _scintilla.TextLength == 0)
            return;

        var (startPos, endPos) = GetVisibleRange();
        if (endPos <= startPos)
            return;

        _scintilla.SearchFlags = flags;
        var scanStart = startPos;
        while (scanStart < endPos)
        {
            _scintilla.TargetStart = scanStart;
            _scintilla.TargetEnd = endPos;
            var foundPosition = _scintilla.SearchInTarget(pattern);
            if (foundPosition < 0)
                break;

            var matchStart = _scintilla.TargetStart;
            var matchEnd = _scintilla.TargetEnd;
            var matchLength = matchEnd - matchStart;
            if (matchLength <= 0)
                break;

            _scintilla.IndicatorCurrent = SearchIndicatorId;
            _scintilla.IndicatorFillRange(matchStart, matchLength);
            scanStart = Math.Max(matchEnd, matchStart + 1);
        }
    }

    private void ClearAllHighlight()
    {
        _scintilla.IndicatorCurrent = SearchIndicatorId;
        _scintilla.IndicatorClearRange(0, _scintilla.TextLength);
    }

    public void ApplyTheme(ScintillaTheme theme)
    {
        ConfigureSearchIndicator(theme);
    }

    private void ConfigureSearchIndicator(ScintillaTheme theme)
    {
        var indicator = _scintilla.Indicators[SearchIndicatorId];
        indicator.Style = IndicatorStyle.RoundBox;
        indicator.ForeColor = theme.SearchHighlight;
        indicator.Under = true;
        indicator.Alpha = theme.IsDarkTheme ? 70 : 55;
        indicator.OutlineAlpha = theme.IsDarkTheme ? 170 : 140;
    }

    private static SearchFlags BuildSearchFlags(bool matchCase, bool useRegex)
    {
        var flags = SearchFlags.None;
        if (matchCase)
            flags |= SearchFlags.MatchCase;
        if (useRegex)
            flags |= SearchFlags.Regex;

        return flags;
    }

    private void SetActiveSearch(string pattern, bool matchCase, bool useRegex)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            ClearSearchHighlight();
            return;
        }

        _activePattern = pattern;
        _activeFlags = BuildSearchFlags(matchCase, useRegex);
        _hasActiveSearch = true;
    }

    private (int Start, int End) GetVisibleRange()
    {
        if (_scintilla.TextLength == 0)
            return (0, 0);

        var topPos = PositionFromPoint(0, 0);
        var rect = _scintilla.ClientRectangle;
        var bottomPos = PositionFromPoint(rect.Width - 1, rect.Height - 1);

        const int buffer = 512;
        var start = Math.Max(0, topPos - buffer);
        var end = Math.Min(_scintilla.TextLength, bottomPos + buffer);
        return (start, end);
    }

    private int PositionFromPoint(int x, int y)
    {
        return _scintilla.DirectMessage(SciPositionFromPoint, (IntPtr)x, (IntPtr)y).ToInt32();
    }

    private static int Clamp(int value, int min, int max)
    {
#if NET8_0_OR_GREATER
        return Math.Clamp(value, min, max);
#else
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
#endif
    }
}

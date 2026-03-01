using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Text;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Render;
using RevitDevTool.Scintilla.Search;
using ScintillaNET;
namespace RevitDevTool.Scintilla.Services;

public sealed class ScintillaDocumentBackend : ILogDocumentBackend
{
    private readonly ScintillaNET.Scintilla _scintilla;
    private readonly ILogStyleRegistry _styleRegistry;
    private readonly ScintillaSearchService _searchService;
    private readonly ScintillaInteractionService _interactionService;
    private readonly ScintillaStyleApplicator _styleApplicator;
    private readonly ScintillaAppendService _appendService;
    private readonly ILogThemeProvider _themeProvider;
    private readonly List<PendingStyleRange> _styleRanges = new(1024);
    private readonly List<RenderSegment> _segments = new(256);
    private readonly List<TokenRange> _tokenRanges = new(2048);
    private readonly Dictionary<int, int> _linkStyleByBaseStyleId = new();
    private long _lastSearchRefreshTick;
    private static readonly byte[] NewLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);
    private const int SciAppendText = 2282;
    private const int SciSetUndoCollection = 2012;
    private const int SearchRefreshThrottleMs = 40;

    public ScintillaDocumentBackend(
        ScintillaNET.Scintilla scintilla,
        ScintillaLogViewerOptions? options = null,
        ILogThemeProvider? themeProvider = null)
    {
        _scintilla = scintilla;
        var resolvedOptions = options ?? new ScintillaLogViewerOptions();
        _themeProvider = themeProvider ?? resolvedOptions.ThemeProvider ?? new StaticLogThemeProvider(resolvedOptions.Theme);
        _styleRegistry = resolvedOptions.StyleRegistry ?? DefaultLogStyleRegistry.Instance;
        _searchService = new ScintillaSearchService(_scintilla);
        _interactionService = new ScintillaInteractionService(_scintilla, resolvedOptions, _tokenRanges);
        _styleApplicator = new ScintillaStyleApplicator(_scintilla, resolvedOptions, _styleRegistry, _tokenRanges, _linkStyleByBaseStyleId);
        _appendService = new ScintillaAppendService(_scintilla);
        ConfigureControl();
        _searchService.ApplyTheme(_themeProvider.CurrentTheme);
        _scintilla.DoubleClick += OnDoubleClick;
        _scintilla.MouseUp += OnMouseUp;
        _scintilla.MouseMove += OnMouseMove;
        _scintilla.MouseLeave += OnMouseLeave;
        _scintilla.MouseWheel += OnMouseWheel;
        _scintilla.UpdateUI += OnUpdateUi;
        _scintilla.SizeChanged += OnSizeChanged;
    }

    public void ConfigureStyles(ILogRenderStrategy renderStrategy)
    {
        _scintilla.StyleResetDefault();
        renderStrategy.ConfigureStyles(new StyleWriter(_scintilla));
        _styleApplicator.RefreshLinkStylesFromBaseStyles();
        _searchService.ApplyTheme(_themeProvider.CurrentTheme);
        RefreshSearchHighlightIfActive();
    }

    public void AppendBatch(IReadOnlyList<LogEntry> entries, ILogRenderStrategy renderStrategy, bool autoScroll)
    {
        if (entries.Count == 0)
            return;

        var wasReadOnly = BeginMutableSection();
        var appendStart = 0;
        var appendedLength = 0;
        try
        {
            (appendStart, appendedLength) = AppendRaw(entries, renderStrategy, autoScroll);
        }
        finally
        {
            EndMutableSection(wasReadOnly);
        }

        RefreshSearchHighlightIfActive(appendStart, appendedLength);
    }

    public void TrimHeadLines(int linesToRemove)
    {
        if (linesToRemove <= 0 || _scintilla.Lines.Count == 0)
            return;

        var wasReadOnly = BeginMutableSection();
        try
        {
            var lastLineToRemove = Math.Min(linesToRemove, _scintilla.Lines.Count - 1);
            if (lastLineToRemove <= 0)
                return;

            var endPos = _scintilla.Lines[lastLineToRemove].Position;
            if (endPos > 0)
                _scintilla.DeleteRange(0, endPos);
            _tokenRanges.Clear();
        }
        finally
        {
            EndMutableSection(wasReadOnly);
        }

        RefreshSearchHighlightIfActive();
    }

    public int GetLineCount() => _scintilla.Lines.Count;

    public void Clear()
        => Clear(ClearMode.Fast);

    public void Clear(ClearMode mode)
    {
        if (mode == ClearMode.Aggressive)
            ClearAggressive();
        else
            ClearFast();
    }

    private void ClearFast()
    {
        var wasReadOnly = BeginMutableSection();
        try
        {
            _scintilla.ClearAll();
            _scintilla.EmptyUndoBuffer();
            _tokenRanges.Clear();
        }
        finally
        {
            EndMutableSection(wasReadOnly);
        }

        RefreshSearchHighlightIfActive();
    }

    private void ClearAggressive()
    {
        var wasReadOnly = BeginMutableSection();
        try
        {
            var newDocument = _scintilla.CreateDocument();
            _scintilla.Document = newDocument;
            _scintilla.ReleaseDocument(newDocument);
            _scintilla.EmptyUndoBuffer();
            _tokenRanges.Clear();
        }
        finally
        {
            EndMutableSection(wasReadOnly);
        }
        _appendService.ResetLargeBuffer();
        TryTrimWorkingSet();
        RefreshSearchHighlightIfActive();
    }

    public LogSearchResult FindNext(string pattern, bool matchCase, bool useRegex)
        => _searchService.FindNext(pattern, matchCase, useRegex);

    public LogSearchResult FindPrevious(string pattern, bool matchCase, bool useRegex)
        => _searchService.FindPrevious(pattern, matchCase, useRegex);

    public LogSearchResult HighlightSearch(string pattern, bool matchCase, bool useRegex)
        => _searchService.HighlightSearch(pattern, matchCase, useRegex);

    public async Task<int> CountMatchesAsync(
        string pattern,
        bool matchCase,
        bool useRegex,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return 0;

        if (_scintilla.IsDisposed)
            return 0;

        var snapshot = await CaptureTextSnapshotAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(snapshot))
            return 0;

        return await Task.Run(() => CountMatches(snapshot, pattern, matchCase, useRegex, cancellationToken), cancellationToken);
    }

    public void Dispose()
    {
        _appendService.Dispose();
        _scintilla.DoubleClick -= OnDoubleClick;
        _scintilla.MouseUp -= OnMouseUp;
        _scintilla.MouseMove -= OnMouseMove;
        _scintilla.MouseLeave -= OnMouseLeave;
        _scintilla.MouseWheel -= OnMouseWheel;
        _scintilla.UpdateUI -= OnUpdateUi;
        _scintilla.SizeChanged -= OnSizeChanged;
        _scintilla.Dispose();
    }

    private void ConfigureControl()
    {
        _scintilla.WrapMode = WrapMode.Word;
        _scintilla.ReadOnly = true;
        _scintilla.DirectMessage(SciSetUndoCollection, IntPtr.Zero, IntPtr.Zero);
        _scintilla.EmptyUndoBuffer();
        _scintilla.IndentationGuides = IndentView.None;
        _scintilla.Margins[0].Width = 0;
        _scintilla.Margins[1].Width = 0;
        _scintilla.Margins[2].Width = 0;

        var indicator = _scintilla.Indicators[_styleRegistry.GetTokenIndicatorStyleId()];
        indicator.Style = IndicatorStyle.RoundBox;
        indicator.ForeColor = _styleRegistry.TokenIndicatorColor;
        indicator.Under = _styleRegistry.TokenIndicatorUnderText;

        var hotspotStyle = _scintilla.Styles[_styleRegistry.GetLinkHotspotStyleId()];
        hotspotStyle.Hotspot = true;
        hotspotStyle.Underline = true;
    }

    private bool BeginMutableSection()
    {
        var wasReadOnly = _scintilla.ReadOnly;
        if (wasReadOnly)
            _scintilla.ReadOnly = false;
        return wasReadOnly;
    }

    private void EndMutableSection(bool wasReadOnly)
    {
        if (wasReadOnly)
            _scintilla.ReadOnly = true;
    }

    private (int Start, int Length) AppendRaw(IReadOnlyList<LogEntry> entries, ILogRenderStrategy renderStrategy, bool autoScroll)
    {
        _styleRanges.Clear();
        _segments.Clear();
        var segmentRenderStrategy = renderStrategy as ISegmentLogRenderStrategy;
        var written = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var message = entry.Message;
            byte[]? formattedLine = null;
            if (segmentRenderStrategy is not null && segmentRenderStrategy.TryFormatLine(entry, out var rewrittenLine))
            {
                formattedLine = rewrittenLine;
                message = new ArraySegment<byte>(rewrittenLine, 0, rewrittenLine.Length);
            }

            var lineByteCount = message.Count;
            var totalByteCount = lineByteCount + NewLineBytes.Length;
            _appendService.EnsureCapacity(written + totalByteCount, written);

            if (lineByteCount > 0 && message.Array is not null)
                Buffer.BlockCopy(message.Array, message.Offset, _appendService.Buffer, written, lineByteCount);
            written += lineByteCount;

            if (segmentRenderStrategy is not null)
            {
                segmentRenderStrategy.BuildSegments(entry, _segments);
                _styleApplicator.AppendStyleRangesFromSegments(_segments, entry.Level, lineByteCount, _styleRanges);
            }
            else
            {
                _styleRanges.Add(new PendingStyleRange(lineByteCount, renderStrategy.GetStyleId(entry.Level), null, false));
            }

            Buffer.BlockCopy(NewLineBytes, 0, _appendService.Buffer, written, NewLineBytes.Length);
            written += NewLineBytes.Length;
            _styleRanges.Add(new PendingStyleRange(NewLineBytes.Length, _styleRegistry.GetStyleId(LogSemanticStyle.SecondaryText), null, false));

        }

        var startPos = _scintilla.TextLength;
        _appendService.Append(written, SciAppendText);
        _styleApplicator.ApplyStylesAndTokenRanges(startPos, written, autoScroll, _styleRanges);
        _interactionService.TrimTokenRangesIfNeeded(50_000);
        return (startPos, written);
    }

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr process);

    private static void TryTrimWorkingSet()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            _ = EmptyWorkingSet(process.Handle);
        }
        catch
        {
            // Best-effort optimization for explicit aggressive clear.
        }
    }

    private void OnDoubleClick(object? sender, EventArgs e) => _interactionService.HandleDoubleClick();

    private void OnMouseUp(object? sender, MouseEventArgs e) => _interactionService.HandleMouseUp(e);

    private void OnMouseMove(object? sender, MouseEventArgs e) => _interactionService.HandleMouseMove(e);

    private void OnMouseLeave(object? sender, EventArgs e) => _interactionService.HandleMouseLeave();

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        if (_searchService.HasActiveSearch)
            RefreshSearchHighlightIfActive(force: true);
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        if (_searchService.HasActiveSearch)
            RefreshSearchHighlightIfActive(force: true);
    }

    private void OnUpdateUi(object? sender, UpdateUIEventArgs e)
    {
        if (!_searchService.HasActiveSearch)
            return;

        if ((e.Change & (UpdateChange.VScroll | UpdateChange.HScroll)) == 0)
            return;

        RefreshSearchHighlightIfActive(force: true);
    }

    private void RefreshSearchHighlightIfActive(int appendStart = 0, int appendedLength = 0, bool force = false)
    {
        if (!_searchService.HasActiveSearch)
            return;

        var now = (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);
        if (!force && now - _lastSearchRefreshTick < SearchRefreshThrottleMs)
            return;

        _lastSearchRefreshTick = now;

        if (appendedLength > 0)
        {
            _searchService.RefreshHighlightForAppendedRange(appendStart, appendedLength);
            return;
        }

        _searchService.RefreshHighlightIfActive();
    }

    private static int CountMatches(
        string text,
        string pattern,
        bool matchCase,
        bool useRegex,
        CancellationToken cancellationToken)
    {
        if (useRegex)
        {
            var options = RegexOptions.CultureInvariant;
            if (!matchCase)
                options |= RegexOptions.IgnoreCase;

            var matches = 0;
            foreach (Match match in Regex.Matches(text, pattern, options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (match.Success && match.Length > 0)
                    matches++;
            }

            return matches;
        }

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var count = 0;
        var index = 0;
        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var found = text.IndexOf(pattern, index, comparison);
            if (found < 0)
                break;

            count++;
            index = found + Math.Max(pattern.Length, 1);
        }

        return count;
    }

    private Task<string> CaptureTextSnapshotAsync(CancellationToken cancellationToken)
    {
        if (!_scintilla.InvokeRequired)
            return Task.FromResult(_scintilla.Text);

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken))
            : default;

        try
        {
            _scintilla.BeginInvoke(new Action(() =>
            {
                try
                {
                    tcs.TrySetResult(_scintilla.IsDisposed ? string.Empty : _scintilla.Text);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    registration.Dispose();
                }
            }));
        }
        catch (Exception ex)
        {
            registration.Dispose();
            tcs.TrySetException(ex);
        }

        return tcs.Task;
    }

    private sealed class StyleWriter(ScintillaNET.Scintilla scintilla) : IStyleWriter
    {
        public void SetDefaultStyle(string fontName, int fontSize, Color foreColor, Color backColor)
        {
            scintilla.Styles[ScintillaNET.Style.Default].Font = fontName;
            scintilla.Styles[ScintillaNET.Style.Default].Size = fontSize;
            scintilla.Styles[ScintillaNET.Style.Default].ForeColor = foreColor;
            scintilla.Styles[ScintillaNET.Style.Default].BackColor = backColor;
            scintilla.StyleClearAll();
        }

        public void SetStyle(int styleId, Color foreColor, Color backColor, bool bold = false)
        {
            scintilla.Styles[styleId].ForeColor = foreColor;
            scintilla.Styles[styleId].BackColor = backColor;
            scintilla.Styles[styleId].Bold = bold;
        }
    }
}

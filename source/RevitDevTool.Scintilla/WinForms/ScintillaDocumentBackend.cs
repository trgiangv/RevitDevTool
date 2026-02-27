using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using RevitDevTool.Scintilla.Contracts;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Search;
using ScintillaNET;

namespace RevitDevTool.Scintilla.WinForms;

public sealed class ScintillaDocumentBackend : ILogDocumentBackend
{
    private readonly ScintillaNET.Scintilla _scintilla;
    private readonly StringBuilder _batchBuilder = new(64 * 1024);
    private readonly List<(int Length, int StyleId)> _styleRanges = new(1024);
#if NET8_0_OR_GREATER
    private byte[] _utf8BatchBuffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
    private const int SciAppendText = 2282;
    private const int SciSetUndoCollection = 2012;
    private static readonly byte[] NewLineUtf8 = Encoding.UTF8.GetBytes(Environment.NewLine);
#endif

    public ScintillaDocumentBackend(ScintillaNET.Scintilla scintilla)
    {
        _scintilla = scintilla;
        ConfigureControl();
    }

    public void ConfigureStyles(ILogRenderStrategy renderStrategy)
    {
        _scintilla.StyleResetDefault();
        renderStrategy.ConfigureStyles(new ScintillaStyleWriter(_scintilla));
    }

    public void AppendBatch(IReadOnlyList<LogEntry> entries, ILogRenderStrategy renderStrategy, bool autoScroll)
    {
        if (entries.Count == 0)
            return;

        var wasReadOnly = BeginMutableSection();
        try
        {
#if NET8_0_OR_GREATER
            AppendBatchUtf8(entries, renderStrategy, autoScroll);
#else
            _styleRanges.Clear();
            _batchBuilder.Clear();

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var line = renderStrategy.FormatLine(entry);
                _batchBuilder.Append(line);
                _batchBuilder.AppendLine();
                _styleRanges.Add((line.Length + Environment.NewLine.Length, renderStrategy.GetStyleId(entry.Level)));
            }

            var startPos = _scintilla.TextLength;
            _scintilla.AppendText(_batchBuilder.ToString());
            _scintilla.StartStyling(startPos);

            for (var i = 0; i < _styleRanges.Count; i++)
            {
                var range = _styleRanges[i];
                _scintilla.SetStyling(range.Length, range.StyleId);
            }

            if (autoScroll)
            {
                _scintilla.GotoPosition(_scintilla.TextLength);
                _scintilla.ScrollCaret();
            }
#endif
        }
        finally
        {
            EndMutableSection(wasReadOnly);
        }
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
        }
        finally
        {
            EndMutableSection(wasReadOnly);
        }
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
        }
        finally
        {
            EndMutableSection(wasReadOnly);
        }
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
        }
        finally
        {
            EndMutableSection(wasReadOnly);
        }
#if NET8_0_OR_GREATER
        if (_utf8BatchBuffer.Length > 256 * 1024)
        {
            ArrayPool<byte>.Shared.Return(_utf8BatchBuffer);
            _utf8BatchBuffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
        }
#endif
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        TryTrimWorkingSet();
    }

    public LogSearchResult FindNext(string pattern, bool matchCase, bool useRegex)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return LogSearchResult.NotFound;

        if (_scintilla.TextLength == 0)
            return LogSearchResult.NotFound;

        var start = Clamp(_scintilla.CurrentPosition, 0, _scintilla.TextLength);
        var flags = SearchFlags.None;
        if (matchCase)
            flags |= SearchFlags.MatchCase;
        if (useRegex)
            flags |= SearchFlags.Regex;

        var found = SearchWithWrap(pattern, flags, start, _scintilla.TextLength);
        if (!found.Found)
            return LogSearchResult.NotFound;

        _scintilla.SetSelection(found.Position + found.Length, found.Position);
        _scintilla.ScrollCaret();
        return new LogSearchResult(true, found.Position, found.Length);
    }

    public void Dispose()
    {
#if NET8_0_OR_GREATER
        ArrayPool<byte>.Shared.Return(_utf8BatchBuffer);
        _utf8BatchBuffer = Array.Empty<byte>();
#endif
        _scintilla.Dispose();
    }

    private void ConfigureControl()
    {
        _scintilla.WrapMode = WrapMode.Word;
        _scintilla.ReadOnly = true;
#if NET8_0_OR_GREATER
        _scintilla.DirectMessage(SciSetUndoCollection, IntPtr.Zero, IntPtr.Zero);
#else
        _scintilla.DirectMessage(2012, IntPtr.Zero, IntPtr.Zero);
#endif
        _scintilla.EmptyUndoBuffer();
        _scintilla.IndentationGuides = IndentView.None;
        _scintilla.Margins[0].Width = 0;
        _scintilla.Margins[1].Width = 0;
        _scintilla.Margins[2].Width = 0;
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

#if NET8_0_OR_GREATER
    private void AppendBatchUtf8(IReadOnlyList<LogEntry> entries, ILogRenderStrategy renderStrategy, bool autoScroll)
    {
        _styleRanges.Clear();
        var written = 0;
        var utf8Strategy = renderStrategy as IUtf8LogRenderStrategy;

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            int lineByteCount;
            if (utf8Strategy is not null)
            {
                lineByteCount = utf8Strategy.GetLineUtf8ByteCount(entry);
                var totalByteCount = lineByteCount + NewLineUtf8.Length;
                EnsureUtf8Capacity(written + totalByteCount, written);
                var lineBytesWritten = utf8Strategy.WriteLineUtf8(entry, _utf8BatchBuffer.AsSpan(written));
                written += lineBytesWritten;
            }
            else
            {
                var line = renderStrategy.FormatLine(entry);
                lineByteCount = Encoding.UTF8.GetByteCount(line);
                var totalByteCount = lineByteCount + NewLineUtf8.Length;
                EnsureUtf8Capacity(written + totalByteCount, written);
                var lineBytesWritten = Encoding.UTF8.GetBytes(line, _utf8BatchBuffer.AsSpan(written));
                written += lineBytesWritten;
            }

            NewLineUtf8.CopyTo(_utf8BatchBuffer.AsSpan(written));
            written += NewLineUtf8.Length;

            _styleRanges.Add((lineByteCount + NewLineUtf8.Length, renderStrategy.GetStyleId(entry.Level)));
        }

        var startPos = _scintilla.TextLength;
        AppendUtf8Bytes(_utf8BatchBuffer, written);
        _scintilla.StartStyling(startPos);

        for (var i = 0; i < _styleRanges.Count; i++)
        {
            var range = _styleRanges[i];
            _scintilla.SetStyling(range.Length, range.StyleId);
        }

        if (autoScroll)
        {
            _scintilla.GotoPosition(_scintilla.TextLength);
            _scintilla.ScrollCaret();
        }
    }

    private void EnsureUtf8Capacity(int requiredSize, int bytesUsed)
    {
        if (requiredSize <= _utf8BatchBuffer.Length)
            return;

        var newSize = _utf8BatchBuffer.Length;
        while (newSize < requiredSize)
            newSize *= 2;

        var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        Buffer.BlockCopy(_utf8BatchBuffer, 0, newBuffer, 0, bytesUsed);
        ArrayPool<byte>.Shared.Return(_utf8BatchBuffer);
        _utf8BatchBuffer = newBuffer;
    }

    private void AppendUtf8Bytes(byte[] buffer, int count)
    {
        if (count <= 0)
            return;

        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var pointer = handle.AddrOfPinnedObject();
            _scintilla.DirectMessage(SciAppendText, (IntPtr)count, pointer);
        }
        finally
        {
            handle.Free();
        }
    }
#endif

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
}

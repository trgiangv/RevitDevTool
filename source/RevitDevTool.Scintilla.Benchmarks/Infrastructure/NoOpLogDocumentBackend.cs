using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Render;
using RevitDevTool.Scintilla.Search;
using RevitDevTool.Scintilla.Services;
using System.Threading;
using System.Threading.Tasks;

namespace RevitDevTool.Scintilla.Benchmarks.Infrastructure;

internal sealed class NoOpLogDocumentBackend : ILogDocumentBackend
{
    private int _lineCount;

    public int LineCount => Volatile.Read(ref _lineCount);

    public void ConfigureStyles(ILogRenderStrategy renderStrategy)
    {
    }

    public void AppendBatch(IReadOnlyList<LogEntry> entries, ILogRenderStrategy renderStrategy, bool autoScroll)
    {
        if (entries.Count > 0)
            Interlocked.Add(ref _lineCount, entries.Count);
    }

    public void TrimHeadLines(int linesToRemove)
    {
        if (linesToRemove <= 0)
            return;

        while (true)
        {
            var current = Volatile.Read(ref _lineCount);
            if (current <= 0)
                return;

            var next = Math.Max(0, current - linesToRemove);
            if (Interlocked.CompareExchange(ref _lineCount, next, current) == current)
                return;
        }
    }

    public int GetLineCount() => Volatile.Read(ref _lineCount);

    public void Clear() => Interlocked.Exchange(ref _lineCount, 0);

    public void Clear(ClearMode mode) => Interlocked.Exchange(ref _lineCount, 0);

    public LogSearchResult FindNext(string pattern, bool matchCase, bool useRegex) => LogSearchResult.NotFound;

    public LogSearchResult FindPrevious(string pattern, bool matchCase, bool useRegex) => LogSearchResult.NotFound;

    public LogSearchResult HighlightSearch(string pattern, bool matchCase, bool useRegex)
    {
        return LogSearchResult.NotFound;
    }

    public Task<int> CountMatchesAsync(string pattern, bool matchCase, bool useRegex, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    public void Dispose()
    {
    }
}

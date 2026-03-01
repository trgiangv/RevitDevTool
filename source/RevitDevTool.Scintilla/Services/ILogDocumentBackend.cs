using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Render;
using RevitDevTool.Scintilla.Search;

namespace RevitDevTool.Scintilla.Services;

public interface ILogDocumentBackend : IDisposable
{
    void ConfigureStyles(ILogRenderStrategy renderStrategy);
    void AppendBatch(IReadOnlyList<LogEntry> entries, ILogRenderStrategy renderStrategy, bool autoScroll);
    void TrimHeadLines(int linesToRemove);
    int GetLineCount();
    void Clear();
    void Clear(ClearMode mode);
    LogSearchResult FindNext(string pattern, bool matchCase, bool useRegex);
    LogSearchResult FindPrevious(string pattern, bool matchCase, bool useRegex);
    LogSearchResult HighlightSearch(string pattern, bool matchCase, bool useRegex);
    Task<int> CountMatchesAsync(string pattern, bool matchCase, bool useRegex, CancellationToken cancellationToken = default);
}

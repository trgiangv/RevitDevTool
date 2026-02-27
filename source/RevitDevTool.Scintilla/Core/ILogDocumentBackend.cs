using RevitDevTool.Scintilla.Contracts;
using RevitDevTool.Scintilla.Search;

namespace RevitDevTool.Scintilla.Core;

public interface ILogDocumentBackend : IDisposable
{
    void ConfigureStyles(ILogRenderStrategy renderStrategy);
    void AppendBatch(IReadOnlyList<LogEntry> entries, ILogRenderStrategy renderStrategy, bool autoScroll);
    void TrimHeadLines(int linesToRemove);
    int GetLineCount();
    void Clear();
    void Clear(ClearMode mode);
    LogSearchResult FindNext(string pattern, bool matchCase, bool useRegex);
}

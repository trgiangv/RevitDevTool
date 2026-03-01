using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Search;
namespace RevitDevTool.Scintilla.Control;

public interface ILogViewerController : IDisposable, IAsyncDisposable
{
    bool IsRunning { get; }
    long AttemptedWrites { get; }
    long AcceptedWrites { get; }
    long LocalWriteFails { get; }
    long DroppedMessages { get; }
    long DroppedByPolicyEstimate { get; }
    long IngestBacklogEstimate { get; }
    long RenderedMessages { get; }
    long HistoryEntries { get; }

    void Start();
    void Stop();
    void Clear();
    void Clear(ClearMode mode);
    void SetAutoScroll(bool enabled);
    void ApplyFilter(LogFilterOptions filterOptions);
    LogSearchResult FindNext(string pattern, bool matchCase = false, bool useRegex = false);
    LogSearchResult FindPrevious(string pattern, bool matchCase = false, bool useRegex = false);
    LogSearchResult HighlightSearch(string pattern, bool matchCase = false, bool useRegex = false);
    Task<int> CountMatchesAsync(string pattern, bool matchCase = false, bool useRegex = false, CancellationToken cancellationToken = default);
}

using RevitDevTool.Scintilla.Search;

namespace RevitDevTool.Scintilla.Contracts;

public interface ILogViewerController : ILogIngress, IDisposable
{
    bool IsRunning { get; }
    long AttemptedWrites { get; }
    long AcceptedWrites { get; }
    long LocalWriteFails { get; }
    long DroppedByPolicyEstimate { get; }
    long IngestBacklogEstimate { get; }
    long RenderedMessages { get; }
    long PendingMessages { get; }
    long HistoryEntries { get; }

    void Start();
    void Stop();
    void Clear();
    void Clear(ClearMode mode);
    void SetAutoScroll(bool enabled);
    void ApplyFilter(LogFilterOptions filterOptions);
    LogSearchResult FindNext(string pattern, bool matchCase = false, bool useRegex = false);
}

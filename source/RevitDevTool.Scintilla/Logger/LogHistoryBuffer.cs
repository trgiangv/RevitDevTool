using System.Collections.Concurrent;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Search;
namespace RevitDevTool.Scintilla.Logger;

internal sealed class LogHistoryBuffer
{
    private readonly ConcurrentQueue<LogEntry> _history = new();
    private readonly LogIngestMetrics _metrics;
    private readonly int _maxEntries;
    private readonly int _trimChunkSize;

    public LogHistoryBuffer(LogIngestMetrics metrics, int maxEntries, int trimChunkSize)
    {
        _metrics = metrics;
        _maxEntries = maxEntries;
        _trimChunkSize = trimChunkSize;
    }

    public bool IsEnabled => _maxEntries > 0;

    public void Enqueue(LogEntry entry)
    {
        _history.Enqueue(entry);
        _metrics.IncrementHistory();
    }

    public void EnqueueBatch(IReadOnlyList<LogEntry> entries)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            _history.Enqueue(entries[i]);
            _metrics.IncrementHistory();
        }
    }

    public void TrimIfNeeded()
    {
        var overflow = _metrics.HistoryEntries - _maxEntries;
        if (overflow <= 0)
            return;

        var toTrim = (int)Math.Min(int.MaxValue, Math.Max(overflow, _trimChunkSize));
        for (var i = 0; i < toTrim; i++)
        {
            if (!_history.TryDequeue(out var removedEntry))
                break;

            _metrics.DecrementHistory();
            removedEntry.ReleaseBuffer();
        }
    }

    public List<LogEntry> CaptureSnapshot(LogFilterState filterState, CancellationToken cancellationToken = default)
    {
        var snapshot = new List<LogEntry>(EstimateSnapshotCapacity(filterState));
        if (filterState.Filter.IsAll)
        {
            var index = 0;
            foreach (var item in _history)
            {
                if ((index++ & 0xFF) == 0 && cancellationToken.IsCancellationRequested)
                    break;
                snapshot.Add(item);
            }

            return snapshot;
        }

        // Hot path: level-only filtering can skip text/property checks entirely.
        if (!filterState.HasTextFilter && filterState.HasLevelFilter)
        {
            var index = 0;
            foreach (var item in _history)
            {
                if ((index++ & 0xFF) == 0 && cancellationToken.IsCancellationRequested)
                    break;

                if (LogFilterEngine.IsLevelMatchOnly(item, filterState))
                    snapshot.Add(item);
            }

            return snapshot;
        }

        var fullIndex = 0;
        foreach (var item in _history)
        {
            if ((fullIndex++ & 0xFF) == 0 && cancellationToken.IsCancellationRequested)
                break;

            if (LogFilterEngine.IsMatch(item, filterState))
                snapshot.Add(item);
        }

        return snapshot;
    }

    public void DrainAll(Action<LogEntry> onEntry)
    {
        while (_history.TryDequeue(out var entry))
            onEntry(entry);
    }

    private int EstimateSnapshotCapacity(LogFilterState filterState)
    {
        var historyEntries = _metrics.HistoryEntries;
        if (historyEntries <= 0)
            return 0;

        var bounded = historyEntries > int.MaxValue ? int.MaxValue : (int)historyEntries;
        if (filterState.Filter.IsAll)
            return bounded;

        // Level-only filters are commonly selective; avoid huge over-allocation.
        if (!filterState.HasTextFilter && filterState.HasLevelFilter)
            return Math.Max(128, bounded / 4);

        // Text filters are usually selective as well.
        return Math.Max(128, bounded / 3);
    }
}

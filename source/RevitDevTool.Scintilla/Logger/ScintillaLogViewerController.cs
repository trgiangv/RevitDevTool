using System.Collections.Concurrent;
using RevitDevTool.Scintilla.Control;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Render;
using RevitDevTool.Scintilla.Search;
using RevitDevTool.Scintilla.Services;
namespace RevitDevTool.Scintilla.Logger;

public sealed class ScintillaLogViewerController : ILogViewerController, ILogEntrySink
{
    private readonly ILogDocumentBackend _document;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogRenderStrategy _renderStrategy;
    private readonly ScintillaLogViewerOptions _options;
    private readonly LogIngestPump _ingestPump;
    private readonly LogIngestMetrics _metrics = new();
    private readonly LogHistoryBuffer _history;
    private readonly ConcurrentQueue<PendingUiBatch> _pendingUiBatches = new();
    private readonly List<LogEntry> _batchBuffer;
    private readonly List<LogEntry> _filteredBuffer;
    private readonly object _stateGate = new();

    private CancellationTokenSource? _cts;
    private Task? _pumpTask;
    private CancellationTokenSource? _filterCts;
    private long _clearEpoch;
    private long _filterVersion;
    private int _uiDrainScheduled;
    private bool _autoScroll;
    private bool _disposed;
    private LogFilterState _filterState = LogFilterState.All;

    public ScintillaLogViewerController(
        ILogDocumentBackend document,
        IUiDispatcher dispatcher,
        ILogRenderStrategy? renderStrategy = null,
        ScintillaLogViewerOptions? options = null)
    {
        _document = document;
        _dispatcher = dispatcher;
        _renderStrategy = renderStrategy ?? new LogRenderStrategy();
        _options = options ?? new ScintillaLogViewerOptions();
        _autoScroll = _options.AutoScroll;
        _batchBuffer = new List<LogEntry>(_options.MaxBatchSize);
        _filteredBuffer = new List<LogEntry>(_options.MaxBatchSize);
        _ingestPump = new LogIngestPump(_options);

        var maxHistory = _options.DisableHistory ? 0 : Math.Max(0, _options.MaxHistoryEntries);
        _history = new LogHistoryBuffer(_metrics, maxHistory, _options.TrimChunkLines);

        Dispatch(() => _document.ConfigureStyles(_renderStrategy));
    }

    public bool IsRunning => _pumpTask is { IsCompleted: false };
    public long AttemptedWrites => _metrics.AttemptedWrites;
    public long AcceptedWrites => _metrics.AcceptedWrites;
    public long LocalWriteFails => _metrics.LocalWriteFails;
    public long DroppedByPolicyEstimate => _metrics.DroppedByPolicyEstimate;
    public long IngestBacklogEstimate => _metrics.IngestBacklogEstimate;
    public long DroppedMessages => _metrics.DroppedMessages;
    public long RenderedMessages => _metrics.RenderedMessages;
    public long PendingMessages => _metrics.IngestBacklogEstimate;
    public long HistoryEntries => _metrics.HistoryEntries;

    public void Start()
    {
        ThrowIfDisposed();

        lock (_stateGate)
        {
            if (_cts != null)
                return;

            _cts = new CancellationTokenSource();
            _pumpTask = Task.Run(() => PumpAsync(_cts.Token), _cts.Token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? pumpTask;

        lock (_stateGate)
        {
            cts = _cts;
            pumpTask = _pumpTask;
            _cts = null;
            _pumpTask = null;
        }

        if (cts == null)
            return;

        cts.Cancel();
        try
        {
            pumpTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) { }
        finally
        {
            cts.Dispose();
        }
    }

    bool ILogEntrySink.TryPost(LogEntry entry)
    {
        ThrowIfDisposed();
        _metrics.RecordAttempt();

        if (_ingestPump.TryPost(entry) == IngestWriteResult.Accepted)
        {
            _metrics.RecordAccepted();
            return true;
        }

        _metrics.RecordDrop();
        entry.ReleaseBuffer();
        return false;
    }

    public void Clear() => Clear(ClearMode.Fast);

    public void Clear(ClearMode mode)
    {
        ThrowIfDisposed();
        Interlocked.Increment(ref _clearEpoch);

        _history.DrainAll(static e => e.ReleaseBuffer());
        _ingestPump.Drain(static e => e.ReleaseBuffer());
        while (_pendingUiBatches.TryDequeue(out var batch))
            batch.ReleaseEntries();

        _metrics.ResetOnClear();
        Dispatch(() => _document.Clear(mode));
    }

    public void SetAutoScroll(bool enabled) => _autoScroll = enabled;

    public void ApplyFilter(LogFilterOptions filterOptions)
    {
        ThrowIfDisposed();

        var filterState = LogFilterEngine.CreateState(filterOptions);
        if (LogFilterEngine.AreEquivalent(_filterState.Filter, filterState.Filter))
            return;

        Interlocked.Exchange(ref _filterState, filterState);
        var currentVersion = Interlocked.Increment(ref _filterVersion);
        var clearEpoch = Interlocked.Read(ref _clearEpoch);
        var nextFilterCts = new CancellationTokenSource();
        var previousFilterCts = Interlocked.Exchange(ref _filterCts, nextFilterCts);
        previousFilterCts?.Cancel();
        previousFilterCts?.Dispose();
        var filterToken = nextFilterCts.Token;

        Task.Run(() =>
        {
            if (filterToken.IsCancellationRequested)
                return;

            var snapshot = _history.CaptureSnapshot(filterState, filterToken);
            if (filterToken.IsCancellationRequested)
                return;

            _dispatcher.BeginInvoke(() => ApplyFilterSnapshot(snapshot, currentVersion, clearEpoch));
        }, filterToken);
    }

    public LogSearchResult FindNext(string pattern, bool matchCase = false, bool useRegex = false)
    {
        ThrowIfDisposed();
        var result = LogSearchResult.NotFound;
        Dispatch(() => result = _document.FindNext(pattern, matchCase, useRegex));
        return result;
    }

    public LogSearchResult FindPrevious(string pattern, bool matchCase = false, bool useRegex = false)
    {
        ThrowIfDisposed();
        var result = LogSearchResult.NotFound;
        Dispatch(() => result = _document.FindPrevious(pattern, matchCase, useRegex));
        return result;
    }

    public LogSearchResult HighlightSearch(string pattern, bool matchCase = false, bool useRegex = false)
    {
        ThrowIfDisposed();
        var result = LogSearchResult.NotFound;
        Dispatch(() => result = _document.HighlightSearch(pattern, matchCase, useRegex));
        return result;
    }

    public Task<int> CountMatchesAsync(
        string pattern,
        bool matchCase = false,
        bool useRegex = false,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _document.CountMatchesAsync(pattern, matchCase, useRegex, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        DisposeCore();
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return default;

        Stop();
        DisposeCore();
        return default;
    }

    private void DisposeCore()
    {
        var filterCts = Interlocked.Exchange(ref _filterCts, null);
        filterCts?.Cancel();
        filterCts?.Dispose();

        _ingestPump.Drain(static e => e.ReleaseBuffer());
        _history.DrainAll(static e => e.ReleaseBuffer());
        while (_pendingUiBatches.TryDequeue(out var batch))
            batch.ReleaseEntries();

        _ingestPump.Complete();
        _document.Dispose();
        _disposed = true;
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.FlushIntervalMs));
        while (!cancellationToken.IsCancellationRequested)
        {
            await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
            ProcessBatch();
        }
#else
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(_options.FlushIntervalMs, cancellationToken).ConfigureAwait(false);
            ProcessBatch();
        }
#endif
    }

    private void ProcessBatch()
    {
        var batchEpoch = Interlocked.Read(ref _clearEpoch);
        var filterState = _filterState;
        var readCount = _ingestPump.TryReadBatch(_batchBuffer);

        if (readCount <= 0)
            return;

        _metrics.DecrementBacklog(readCount);

        if (batchEpoch != Interlocked.Read(ref _clearEpoch))
        {
            ReleaseBatchEntries(_batchBuffer);
            return;
        }

        if (_history.IsEnabled)
        {
            _history.EnqueueBatch(_batchBuffer);
            _history.TrimIfNeeded();
        }

        var visibleEntries = SelectVisibleEntries(filterState);

        if (!_history.IsEnabled)
        {
            QueueWithoutHistory(batchEpoch, visibleEntries);
            return;
        }

        if (visibleEntries.Count > 0)
            EnqueuePendingUiBatch(new PendingUiBatch(batchEpoch, EntryBuffer.Empty, visibleEntries, _autoScroll));
        else
            visibleEntries.Return();
    }

    private void ApplyFilterSnapshot(List<LogEntry> localBuffer, long currentVersion, long clearEpoch)
    {
        if (_disposed)
            return;
        if (currentVersion != Interlocked.Read(ref _filterVersion))
            return;
        if (clearEpoch != Interlocked.Read(ref _clearEpoch))
            return;

        _document.Clear();
        if (localBuffer.Count > 0)
        {
            _document.AppendBatch(localBuffer, _renderStrategy, _autoScroll);
            _metrics.RecordRendered(localBuffer.Count);
        }

        TrimDocumentIfNeeded();
    }

    private EntryBuffer SelectVisibleEntries(LogFilterState filterState)
    {
        if (filterState.Filter.IsAll)
            return EntryBuffer.CopyFrom(_batchBuffer);

        _filteredBuffer.Clear();
        for (var i = 0; i < _batchBuffer.Count; i++)
        {
            if (LogFilterEngine.IsMatch(_batchBuffer[i], filterState))
                _filteredBuffer.Add(_batchBuffer[i]);
        }

        return EntryBuffer.CopyFrom(_filteredBuffer);
    }

    private void QueueWithoutHistory(long batchEpoch, EntryBuffer visibleEntries)
    {
        if (visibleEntries.Count == 0)
        {
            visibleEntries.Return();
            ReleaseBatchEntries(_batchBuffer);
            return;
        }

        var releaseEntries = visibleEntries.Count == _batchBuffer.Count
            ? visibleEntries
            : EntryBuffer.CopyFrom(_batchBuffer);

        EnqueuePendingUiBatch(new PendingUiBatch(batchEpoch, releaseEntries, visibleEntries, _autoScroll));
    }

    private void EnqueuePendingUiBatch(PendingUiBatch batch)
    {
        _pendingUiBatches.Enqueue(batch);

        if (Interlocked.Exchange(ref _uiDrainScheduled, 1) == 1)
            return;

        _dispatcher.BeginInvoke(DrainPendingUiBatches);
    }

    private void DrainPendingUiBatches()
    {
        if (_disposed)
            return;

        while (true)
        {
            while (_pendingUiBatches.TryDequeue(out var batch))
            {
                try
                {
                    if (batch.Epoch != Interlocked.Read(ref _clearEpoch))
                        continue;

                    if (batch.Count > 0)
                    {
                        _document.AppendBatch(batch, _renderStrategy, batch.AutoScroll);
                        _metrics.RecordRendered(batch.Count);
                    }

                    TrimDocumentIfNeeded();
                }
                finally
                {
                    batch.ReleaseEntries();
                }
            }

            Interlocked.Exchange(ref _uiDrainScheduled, 0);
            if (_pendingUiBatches.IsEmpty || Interlocked.Exchange(ref _uiDrainScheduled, 1) == 1)
                break;
        }
    }

    private void TrimDocumentIfNeeded()
    {
        var lineCount = _document.GetLineCount();
        if (lineCount <= _options.MaxLines)
            return;

        _document.TrimHeadLines(Math.Max(_options.TrimChunkLines, lineCount - _options.MaxLines));
    }

    private void Dispatch(Action action)
    {
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.Invoke(action);
    }

    private void ThrowIfDisposed()
    {
#if NET8_0_OR_GREATER
        ObjectDisposedException.ThrowIf(_disposed, this);
#else
        if (_disposed)
            throw new ObjectDisposedException(GetType().FullName);
#endif
    }
    private static void ReleaseBatchEntries(IReadOnlyList<LogEntry> entries)
    {
        for (var i = 0; i < entries.Count; i++)
            entries[i].ReleaseBuffer();
    }
}

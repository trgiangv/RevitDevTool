using System.Collections.Concurrent;
using System.Threading.Channels;
using RevitDevTool.Scintilla.Contracts;
using RevitDevTool.Scintilla.Search;

namespace RevitDevTool.Scintilla.Core;

public sealed class ScintillaLogViewerController : ILogViewerController
{
    private readonly ILogDocumentBackend _document;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogRenderStrategy _renderStrategy;
    private readonly ScintillaLogViewerOptions _options;
    private readonly Channel<LogEntry> _channel;
    private readonly ConcurrentQueue<LogEntry> _history = new();
    private readonly List<LogEntry> _batchBuffer;
    private readonly List<LogEntry> _filteredBuffer;
    private readonly object _stateGate = new();
    private readonly int _maxHistoryEntries;

    private CancellationTokenSource? _cts;
    private Task? _pumpTask;
    private long _droppedMessages;
    private long _renderedMessages;
    private long _attemptedWrites;
    private long _acceptedWrites;
    private long _localWriteFails;
    private long _droppedByPolicyEstimate;
    private long _ingestBacklogEstimate;
    private long _historyEntries;
    private bool _autoScroll;
    private bool _disposed;
    private LogFilterOptions _filterOptions = LogFilterOptions.All;

    public ScintillaLogViewerController(
        ILogDocumentBackend document,
        IUiDispatcher dispatcher,
        ILogRenderStrategy? renderStrategy = null,
        ScintillaLogViewerOptions? options = null)
    {
        _document = document;
        _dispatcher = dispatcher;
        _renderStrategy = renderStrategy ?? new DefaultLogRenderStrategy();
        _options = options ?? new ScintillaLogViewerOptions();
        _autoScroll = _options.AutoScroll;
        _maxHistoryEntries = Math.Max(0, _options.MaxHistoryEntries);
        _batchBuffer = new List<LogEntry>(_options.MaxBatchSize);
        _filteredBuffer = new List<LogEntry>(_options.MaxBatchSize);
        _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(_options.ChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = _options.DropPolicy
        });

        Dispatch(() => _document.ConfigureStyles(_renderStrategy));
    }

    public bool IsRunning => _pumpTask is { IsCompleted: false };
    public long AttemptedWrites => Interlocked.Read(ref _attemptedWrites);
    public long AcceptedWrites => Interlocked.Read(ref _acceptedWrites);
    public long LocalWriteFails => Interlocked.Read(ref _localWriteFails);
    public long DroppedByPolicyEstimate => Interlocked.Read(ref _droppedByPolicyEstimate);
    public long IngestBacklogEstimate => Interlocked.Read(ref _ingestBacklogEstimate);
    public long DroppedMessages => Interlocked.Read(ref _droppedMessages);
    public long RenderedMessages => Interlocked.Read(ref _renderedMessages);
    public long PendingMessages => Interlocked.Read(ref _ingestBacklogEstimate);
    public long HistoryEntries => Interlocked.Read(ref _historyEntries);

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
        catch (OperationCanceledException)
        {
            // Expected while stopping.
        }
        finally
        {
            cts.Dispose();
        }
    }

    public bool TryPost(LogEntry entry)
    {
        ThrowIfDisposed();
        Interlocked.Increment(ref _attemptedWrites);
        if (_channel.Writer.TryWrite(entry))
        {
            Interlocked.Increment(ref _acceptedWrites);

            var backlogEstimate = Interlocked.Increment(ref _ingestBacklogEstimate);
            if (backlogEstimate > _options.ChannelCapacity)
            {
                Interlocked.Exchange(ref _ingestBacklogEstimate, _options.ChannelCapacity);
                if (_options.DropPolicy is BoundedChannelFullMode.DropOldest or BoundedChannelFullMode.DropNewest or BoundedChannelFullMode.DropWrite)
                    Interlocked.Increment(ref _droppedByPolicyEstimate);
            }

            return true;
        }

        Interlocked.Increment(ref _localWriteFails);
        Interlocked.Increment(ref _droppedMessages);
        return false;
    }

    public void Clear()
        => Clear(ClearMode.Fast);

    public void Clear(ClearMode mode)
    {
        ThrowIfDisposed();

        while (_history.TryDequeue(out _))
        {
        }
        Interlocked.Exchange(ref _historyEntries, 0);

        Dispatch(() => _document.Clear(mode));
    }

    public void SetAutoScroll(bool enabled) => _autoScroll = enabled;

    public void ApplyFilter(LogFilterOptions filterOptions)
    {
        ThrowIfDisposed();
        _filterOptions = filterOptions ?? LogFilterOptions.All;

        var filtered = new List<LogEntry>();
        if (_filterOptions.IsAll)
        {
            filtered.AddRange(_history);
        }
        else
        {
            foreach (var item in _history)
            {
                if (_filterOptions.IsMatch(item))
                    filtered.Add(item);
            }
        }

        Dispatch(() =>
        {
            _document.Clear();
            if (filtered.Count > 0)
                _document.AppendBatch(filtered, _renderStrategy, _autoScroll);
        });
    }

    public LogSearchResult FindNext(string pattern, bool matchCase = false, bool useRegex = false)
    {
        ThrowIfDisposed();
        var result = LogSearchResult.NotFound;
        Dispatch(() => result = _document.FindNext(pattern, matchCase, useRegex));
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _channel.Writer.TryComplete();
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
        _batchBuffer.Clear();
        while (_batchBuffer.Count < _options.MaxBatchSize &&
               _channel.Reader.TryRead(out var entry))
        {
            _batchBuffer.Add(entry);
            DecrementBacklogEstimate();
        }

        if (_batchBuffer.Count == 0)
            return;

        foreach (var item in _batchBuffer)
        {
            if (_maxHistoryEntries > 0)
            {
                _history.Enqueue(item);
                Interlocked.Increment(ref _historyEntries);
            }
        }
        if (_maxHistoryEntries > 0)
            TrimHistoryIfNeeded();

        IReadOnlyList<LogEntry> visibleBatch;
        if (_filterOptions.IsAll)
        {
            visibleBatch = _batchBuffer;
        }
        else
        {
            _filteredBuffer.Clear();
            for (var i = 0; i < _batchBuffer.Count; i++)
            {
                var item = _batchBuffer[i];
                if (_filterOptions.IsMatch(item))
                    _filteredBuffer.Add(item);
            }
            visibleBatch = _filteredBuffer;
        }

        Dispatch(() =>
        {
            if (visibleBatch.Count > 0)
                _document.AppendBatch(visibleBatch, _renderStrategy, _autoScroll);
            TrimIfNeeded();
        });

        Interlocked.Add(ref _renderedMessages, visibleBatch.Count);
    }

    private void TrimIfNeeded()
    {
        var lineCount = _document.GetLineCount();
        if (lineCount <= _options.MaxLines)
            return;

        var linesToRemove = Math.Max(_options.TrimChunkLines, lineCount - _options.MaxLines);
        _document.TrimHeadLines(linesToRemove);
    }

    private void TrimHistoryIfNeeded()
    {
        var historyEntries = Interlocked.Read(ref _historyEntries);
        var overflow = historyEntries - _maxHistoryEntries;
        if (overflow <= 0)
            return;

        var toTrim = (int)Math.Min(int.MaxValue, Math.Max(overflow, _options.TrimChunkLines));
        for (var i = 0; i < toTrim && _history.TryDequeue(out _); i++)
        {
            Interlocked.Decrement(ref _historyEntries);
        }
    }

    private void Dispatch(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

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

    private void DecrementBacklogEstimate()
    {
        while (true)
        {
            var current = Interlocked.Read(ref _ingestBacklogEstimate);
            if (current <= 0)
                return;

            if (Interlocked.CompareExchange(ref _ingestBacklogEstimate, current - 1, current) == current)
                return;
        }
    }
}

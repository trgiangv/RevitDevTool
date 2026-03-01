namespace RevitDevTool.Scintilla.Logger;

internal sealed class LogIngestMetrics
{
    private long _attemptedWrites;
    private long _acceptedWrites;
    private long _localWriteFails;
    private long _droppedMessages;
    private long _droppedByPolicyEstimate;
    private long _ingestBacklogEstimate;
    private long _renderedMessages;
    private long _historyEntries;

    public long AttemptedWrites => Interlocked.Read(ref _attemptedWrites);
    public long AcceptedWrites => Interlocked.Read(ref _acceptedWrites);
    public long LocalWriteFails => Interlocked.Read(ref _localWriteFails);
    public long DroppedMessages => Interlocked.Read(ref _droppedMessages);
    public long DroppedByPolicyEstimate => Interlocked.Read(ref _droppedByPolicyEstimate);
    public long IngestBacklogEstimate => Interlocked.Read(ref _ingestBacklogEstimate);
    public long RenderedMessages => Interlocked.Read(ref _renderedMessages);
    public long HistoryEntries => Interlocked.Read(ref _historyEntries);

    public void RecordAttempt() => Interlocked.Increment(ref _attemptedWrites);

    public void RecordAccepted()
    {
        Interlocked.Increment(ref _acceptedWrites);
        Interlocked.Increment(ref _ingestBacklogEstimate);
    }

    public void RecordDrop()
    {
        Interlocked.Increment(ref _localWriteFails);
        Interlocked.Increment(ref _droppedMessages);
        Interlocked.Increment(ref _droppedByPolicyEstimate);
    }

    public void RecordRendered(int count)
    {
        if (count > 0)
            Interlocked.Add(ref _renderedMessages, count);
    }

    public void IncrementHistory() => Interlocked.Increment(ref _historyEntries);

    public void DecrementHistory() => Interlocked.Decrement(ref _historyEntries);

    public void DecrementBacklog(int count)
    {
        if (count <= 0)
            return;

        while (true)
        {
            var current = Interlocked.Read(ref _ingestBacklogEstimate);
            if (current <= 0)
                return;

            var next = Math.Max(0, current - count);
            if (Interlocked.CompareExchange(ref _ingestBacklogEstimate, next, current) == current)
                return;
        }
    }

    public void ResetOnClear()
    {
        Interlocked.Exchange(ref _historyEntries, 0);
        Interlocked.Exchange(ref _ingestBacklogEstimate, 0);
    }
}

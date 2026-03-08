using System.Collections.Concurrent;
using System.Diagnostics;
using RevitDevTool.Controllers;
using RevitDevTool.Mcp.Schemas;
namespace RevitDevTool.Mcp;

public sealed class McpExecutionQueue : IDisposable
{
    private readonly Models.McpBridgeState _state;
    private readonly ConcurrentQueue<QueuedExecution> _queue = new();
    private readonly ConcurrentDictionary<string, McpExecutionSnapshot> _executions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, QueuedExecution> _jobsByExecutionId = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _workerTask;
    private int _queueDepth;
    private bool _disposed;

    public McpExecutionQueue(Models.McpBridgeState state)
    {
        _state = state;
        _workerTask = Task.Run(ProcessLoopAsync);
    }

    public Task<McpToolExecutionResult> EnqueueAsync(
        string executionId,
        string toolId,
        string toolName,
        Func<IProgress<McpProgressUpdate>, CancellationToken, Task<McpToolExecutionResult>> executor,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var completion = new TaskCompletionSource<McpToolExecutionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        var job = new QueuedExecution(executionId, toolId, toolName, executor, completion, linkedCancellation);

        _queue.Enqueue(job);
        _jobsByExecutionId[executionId] = job;
        _executions[executionId] = CreateQueuedSnapshot(job);
        var depth = Interlocked.Increment(ref _queueDepth);
        _state.SetQueueDepth(depth);
        _state.RecordQueued(toolName);
        Trace.TraceInformation($"[MCP] Enqueued tool '{toolName}' ({executionId}). Queue depth: {depth}");
        _signal.Release();
        return completion.Task;
    }

    public McpExecutionSnapshot? GetExecutionSnapshot(string executionId)
    {
        if (string.IsNullOrWhiteSpace(executionId))
            return null;

        return _executions.TryGetValue(executionId, out var snapshot) ? snapshot : null;
    }

    public bool TryCancel(string executionId)
    {
        if (string.IsNullOrWhiteSpace(executionId) || !_jobsByExecutionId.TryGetValue(executionId, out var job))
            return false;

        if (job.Cancellation.IsCancellationRequested)
            return true;

        job.Cancellation.Cancel();
        UpdateSnapshot(executionId, snapshot => CloneSnapshot(
            snapshot,
            state: snapshot.State == McpExecutionStates.Queued ? McpExecutionStates.Cancelled : snapshot.State,
            message: string.IsNullOrWhiteSpace(snapshot.Message) ? "Cancellation requested." : snapshot.Message,
            canCancel: false,
            updatedAtUtc: DateTime.UtcNow,
            completedAtUtc: snapshot.State == McpExecutionStates.Queued ? DateTime.UtcNow : snapshot.CompletedAtUtc));
        return true;
    }

    private async Task ProcessLoopAsync()
    {
        while (await WaitForSignalAsync().ConfigureAwait(false))
        {
            if (!TryDequeueJob(out var job) || job is null)
                continue;

            await ProcessJobAsync(job).ConfigureAwait(false);
        }
    }

    private async Task<bool> WaitForSignalAsync()
    {
        try
        {
            await _signal.WaitAsync(_cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private bool TryDequeueJob(out QueuedExecution? job)
    {
        if (!_queue.TryDequeue(out var dequeuedJob))
        {
            job = null;
            return false;
        }

        job = dequeuedJob;
        var depth = Interlocked.Decrement(ref _queueDepth);
        _state.SetQueueDepth(depth);
        Trace.TraceInformation($"[MCP] Dequeued tool '{job.ToolName}' ({job.ExecutionId}). Queue depth: {Math.Max(0, depth)}");
        return true;
    }

    private async Task ProcessJobAsync(QueuedExecution job)
    {
        try
        {
            if (TryCompleteCancelledBeforeStart(job))
                return;

            var progressEvents = new List<McpProgressUpdate>();
            var progress = CreateProgressReporter(job, progressEvents);
            MarkExecutionStarted(job);

            await ExecuteAndCompleteJobAsync(job, progress, progressEvents).ConfigureAwait(false);
        }
        finally
        {
            CleanupJob(job);
        }
    }

    private bool TryCompleteCancelledBeforeStart(QueuedExecution job)
    {
        if (!job.CancellationToken.IsCancellationRequested)
            return false;

        CompleteCancelledJob(job, "Execution cancelled before start.");
        return true;
    }

    private void MarkExecutionStarted(QueuedExecution job)
    {
        _state.StartExecution(job.ToolName, $"Starting '{job.ToolName}'...");
        var startedAtUtc = DateTime.UtcNow;
        UpdateSnapshot(job.ExecutionId, snapshot => CloneSnapshot(
            snapshot,
            state: McpExecutionStates.Running,
            message: $"Starting '{job.ToolName}'...",
            canCancel: true,
            startedAtUtc: startedAtUtc,
            updatedAtUtc: startedAtUtc,
            completedAtUtc: null));
    }

    private async Task ExecuteAndCompleteJobAsync(
        QueuedExecution job,
        IProgress<McpProgressUpdate> progress,
        IReadOnlyList<McpProgressUpdate> progressEvents)
    {
        try
        {
            var result = await ExecuteJobOnRevitThreadAsync(job, progress).ConfigureAwait(false);
            var normalizedResult = EnsureProgressUpdates(result, progressEvents);

            Trace.TraceInformation($"[MCP] Tool '{job.ToolName}' ({job.ExecutionId}) executed. Success={normalizedResult.Success}");
            CompleteJob(job, normalizedResult);
        }
        catch (OperationCanceledException) when (job.CancellationToken.IsCancellationRequested)
        {
            CompleteCancelledJob(job, "Execution cancelled.", progressEvents);
        }
        catch (Exception ex)
        {
            HandleExecutionFailure(job, ex, progressEvents);
        }
    }

    private void HandleExecutionFailure(
        QueuedExecution job,
        Exception ex,
        IReadOnlyList<McpProgressUpdate> progressEvents)
    {
        Trace.TraceError($"[MCP] Queue execution failed for '{job.ToolName}' ({job.ExecutionId}): {ex.Message}");
        _executions.TryGetValue(job.ExecutionId, out var snapshot);
        var failedResult = McpToolExecutionResult.Failed(
            "queue.execution_failed",
            ex.Message,
            ex.StackTrace,
            CreateMetadata(job, snapshot?.StartedAtUtc ?? DateTime.UtcNow, DateTime.UtcNow),
            progressEvents);
        CompleteJob(job, failedResult);
    }

    private void CleanupJob(QueuedExecution job)
    {
        _jobsByExecutionId.TryRemove(job.ExecutionId, out _);
        job.Cancellation.Dispose();
    }

    private static McpExecutionSnapshot CreateQueuedSnapshot(QueuedExecution job)
    {
        var now = DateTime.UtcNow;
        return new McpExecutionSnapshot
        {
            ExecutionId = job.ExecutionId,
            ToolId = job.ToolId,
            ToolName = job.ToolName,
            State = McpExecutionStates.Queued,
            Message = $"Queued '{job.ToolName}'.",
            CanCancel = true,
            StartedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private IProgress<McpProgressUpdate> CreateProgressReporter(QueuedExecution job, List<McpProgressUpdate> progressEvents)
    {
        return new Progress<McpProgressUpdate>(update =>
        {
            lock (progressEvents)
            {
                progressEvents.Add(update);
            }

            _state.ReportProgress(update);
            UpdateSnapshot(job.ExecutionId, snapshot => CloneSnapshot(
                snapshot,
                state: McpExecutionStates.Running,
                message: update.Message,
                updatedAtUtc: DateTime.UtcNow,
                progressUpdates: snapshot.ProgressUpdates.Concat([update]).ToList()));
        });
    }

    private static async Task<McpToolExecutionResult> ExecuteJobOnRevitThreadAsync(
        QueuedExecution job,
        IProgress<McpProgressUpdate> progress)
    {
        var handler = await ExternalEventController
            .AsyncGenericEventHandler<McpToolExecutionResult>()
            .ConfigureAwait(false);
        return await handler
            .RaiseAsync(() => job.Executor(progress, job.CancellationToken).ConfigureAwait(false).GetAwaiter().GetResult())
            .ConfigureAwait(false);
    }

    private void CompleteJob(QueuedExecution job, McpToolExecutionResult result)
    {
        var completedAt = DateTime.UtcNow;
        _executions.TryGetValue(job.ExecutionId, out var snapshot);
        var normalizedResult = EnsureExecutionMetadata(job, result, snapshot, completedAt);
        _state.CompleteExecution(job.ToolName, normalizedResult);
        UpdateSnapshot(job.ExecutionId, snapshotDto => CloneSnapshot(
            snapshotDto,
            state: normalizedResult.IsCancelled
                ? McpExecutionStates.Cancelled
                : normalizedResult.Success
                    ? McpExecutionStates.Completed
                    : McpExecutionStates.Failed,
            message: normalizedResult.Message,
            resultKind: normalizedResult.ResultKind,
            canCancel: false,
            updatedAtUtc: completedAt,
            completedAtUtc: completedAt,
            error: normalizedResult.Error,
            progressUpdates: normalizedResult.ProgressUpdates.ToList()));
        job.Completion.TrySetResult(normalizedResult);
    }

    private void CompleteCancelledJob(QueuedExecution job, string message, IReadOnlyList<McpProgressUpdate>? progressEvents = null)
    {
        _executions.TryGetValue(job.ExecutionId, out var snapshot);
        var cancelledResult = McpToolExecutionResult.Cancelled(
            message,
            CreateMetadata(job, snapshot?.StartedAtUtc ?? DateTime.UtcNow, DateTime.UtcNow),
            progressEvents ?? []);
        CompleteJob(job, cancelledResult);
    }

    private static McpToolExecutionResult EnsureProgressUpdates(
        McpToolExecutionResult result,
        IReadOnlyList<McpProgressUpdate> progressEvents)
    {
        if (result.ProgressUpdates.Count != 0 || progressEvents.Count == 0)
            return result;

        return result.Success
            ? McpToolExecutionResult.Succeeded(
                result.PayloadJson,
                result.Message,
                result.ResultKind,
                result.Metadata,
                progressEvents)
            : result.IsCancelled
                ? McpToolExecutionResult.Cancelled(result.Message, result.Metadata, progressEvents)
                : McpToolExecutionResult.Failed(
                    result.Error?.Code ?? "tool.failed",
                    result.Message,
                    result.Error?.Details,
                    result.Metadata,
                    progressEvents);
    }

    private static McpToolExecutionResult EnsureExecutionMetadata(
        QueuedExecution job,
        McpToolExecutionResult result,
        McpExecutionSnapshot? snapshot,
        DateTime completedAtUtc)
    {
        var existingMetadata = result.Metadata;
        var startedAtUtc = existingMetadata?.StartedAtUtc ?? snapshot?.StartedAtUtc ?? completedAtUtc;

        if (existingMetadata is not null &&
            string.Equals(existingMetadata.ExecutionId, job.ExecutionId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existingMetadata.ToolId, job.ToolId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existingMetadata.ToolName, job.ToolName, StringComparison.Ordinal) &&
            existingMetadata.CompletedAtUtc != default)
        {
            return result;
        }

        var metadata = new McpToolExecutionMetadata
        {
            ExecutionId = string.IsNullOrWhiteSpace(existingMetadata?.ExecutionId) ? job.ExecutionId : existingMetadata!.ExecutionId,
            ToolId = string.IsNullOrWhiteSpace(existingMetadata?.ToolId) ? job.ToolId : existingMetadata!.ToolId,
            ToolName = string.IsNullOrWhiteSpace(existingMetadata?.ToolName) ? job.ToolName : existingMetadata!.ToolName,
            DurationMs = existingMetadata?.DurationMs ?? (int)Math.Max(0, (completedAtUtc - startedAtUtc).TotalMilliseconds),
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc
        };

        return result.Success
            ? McpToolExecutionResult.Succeeded(
                result.PayloadJson,
                result.Message,
                result.ResultKind,
                metadata,
                result.ProgressUpdates)
            : result.IsCancelled
                ? McpToolExecutionResult.Cancelled(result.Message, metadata, result.ProgressUpdates)
                : McpToolExecutionResult.Failed(
                    result.Error?.Code ?? "tool.failed",
                    result.Message,
                    result.Error?.Details,
                    metadata,
                    result.ProgressUpdates);
    }

    private static McpToolExecutionMetadata CreateMetadata(QueuedExecution job, DateTime startedAtUtc, DateTime completedAtUtc)
    {
        return new McpToolExecutionMetadata
        {
            ExecutionId = job.ExecutionId,
            ToolId = job.ToolId,
            ToolName = job.ToolName,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            DurationMs = (int)Math.Max(0, (completedAtUtc - startedAtUtc).TotalMilliseconds)
        };
    }

    private void UpdateSnapshot(string executionId, Func<McpExecutionSnapshot, McpExecutionSnapshot> update)
    {
        _executions.AddOrUpdate(
            executionId,
            _ => update(new McpExecutionSnapshot { ExecutionId = executionId }),
            (_, existing) => update(existing));
    }

    private static McpExecutionSnapshot CloneSnapshot(
        McpExecutionSnapshot snapshot,
        string? state = null,
        string? message = null,
        string? resultKind = null,
        bool? canCancel = null,
        DateTime? startedAtUtc = null,
        DateTime? updatedAtUtc = null,
        DateTime? completedAtUtc = null,
        McpException? error = null,
        IReadOnlyList<McpProgressUpdate>? progressUpdates = null)
    {
        return new McpExecutionSnapshot
        {
            ExecutionId = snapshot.ExecutionId,
            ToolId = snapshot.ToolId,
            ToolName = snapshot.ToolName,
            State = state ?? snapshot.State,
            Message = message ?? snapshot.Message,
            ResultKind = resultKind ?? snapshot.ResultKind,
            CanCancel = canCancel ?? snapshot.CanCancel,
            StartedAtUtc = startedAtUtc ?? snapshot.StartedAtUtc,
            UpdatedAtUtc = updatedAtUtc ?? snapshot.UpdatedAtUtc,
            CompletedAtUtc = completedAtUtc ?? snapshot.CompletedAtUtc,
            Error = error ?? snapshot.Error,
            ProgressUpdates = progressUpdates ?? snapshot.ProgressUpdates
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        try
        {
            _workerTask.GetAwaiter().GetResult();
        }
        catch
        {
            // no-op
        }
        _cts.Dispose();
        _signal.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(McpExecutionQueue));
    }

    private sealed record QueuedExecution(
        string ExecutionId,
        string ToolId,
        string ToolName,
        Func<IProgress<McpProgressUpdate>, CancellationToken, Task<McpToolExecutionResult>> Executor,
        TaskCompletionSource<McpToolExecutionResult> Completion,
        CancellationTokenSource Cancellation)
    {
        public CancellationToken CancellationToken => Cancellation.Token;
    }
}

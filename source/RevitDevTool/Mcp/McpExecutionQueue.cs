using System.Collections.Concurrent;
using System.Diagnostics;
using RevitDevTool.Contracts;
using RevitDevTool.Controllers;
using RevitDevTool.Mcp.Models;
// ReSharper disable RedundantSuppressNullableWarningExpression
namespace RevitDevTool.Mcp;

public sealed class McpExecutionQueue : IDisposable
{
    private readonly McpBridgeState _state;
    private readonly ConcurrentQueue<QueuedExecution> _queue = new();
    private readonly ConcurrentDictionary<string, McpExecutionSnapshot> _executions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, QueuedExecution> _jobsByExecutionId = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _workerTask;
    private int _queueDepth;
    private bool _disposed;

    public McpExecutionQueue(McpBridgeState state)
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
            state: snapshot.State == ExecutionState.Queued ? ExecutionState.Cancelled : snapshot.State,
            detail: string.IsNullOrWhiteSpace(snapshot.Detail) ? "Cancellation requested." : snapshot.Detail,
            canCancel: false,
            updatedAtUtc: DateTime.UtcNow,
            completedAtUtc: snapshot.State == ExecutionState.Queued ? DateTime.UtcNow : snapshot.CompletedAtUtc));
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

            var progress = CreateProgressReporter(job);
            MarkExecutionStarted(job);

            await ExecuteAndCompleteJobAsync(job, progress).ConfigureAwait(false);
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
            state: ExecutionState.Preparing,
            detail: $"Starting '{job.ToolName}'...",
            canCancel: true,
            startedAtUtc: startedAtUtc,
            updatedAtUtc: startedAtUtc,
            completedAtUtc: null));
    }

    private async Task ExecuteAndCompleteJobAsync(
        QueuedExecution job,
        IProgress<McpProgressUpdate> progress)
    {
        try
        {
            var result = await ExecuteJobOnRevitThreadAsync(job, progress).ConfigureAwait(false);
            Trace.TraceInformation($"[MCP] Tool '{job.ToolName}' ({job.ExecutionId}) executed. State={result.State}");
            CompleteJob(job, result);
        }
        catch (OperationCanceledException) when (job.CancellationToken.IsCancellationRequested)
        {
            CompleteCancelledJob(job, "Execution cancelled.");
        }
        catch (Exception ex)
        {
            HandleExecutionFailure(job, ex);
        }
    }

    private void HandleExecutionFailure(
        QueuedExecution job,
        Exception ex)
    {
        Trace.TraceError($"[MCP] Queue execution failed for '{job.ToolName}' ({job.ExecutionId}): {ex.Message}");
        var failedResult = McpToolExecutionResult.Failed(BridgeErrorCodes.QueueExecutionFailed, ex.Message, ex.StackTrace);
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
            State = ExecutionState.Queued,
            Detail = $"Queued '{job.ToolName}'.",
            CanCancel = true,
            StartedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private IProgress<McpProgressUpdate> CreateProgressReporter(QueuedExecution job)
    {
        return new Progress<McpProgressUpdate>(update =>
        {
            _state.ReportProgress(update);
            UpdateSnapshot(job.ExecutionId, snapshot => CloneSnapshot(
                snapshot,
                state: update.State,
                detail: update.Detail,
                updatedAtUtc: DateTime.UtcNow));
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
        _state.CompleteExecution(job.ToolName, result);
        UpdateSnapshot(job.ExecutionId, snapshot => CloneSnapshot(
            snapshot,
            state: result.State,
            detail: result.Detail,
            canCancel: false,
            updatedAtUtc: completedAt,
            completedAtUtc: completedAt,
            error: result.Error));
        job.Completion.TrySetResult(result);
    }

    private void CompleteCancelledJob(QueuedExecution job, string detail)
    {
        var cancelledResult = McpToolExecutionResult.Cancelled(detail);
        CompleteJob(job, cancelledResult);
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
        ExecutionState? state = null,
        string? detail = null,
        bool? canCancel = null,
        DateTime? startedAtUtc = null,
        DateTime? updatedAtUtc = null,
        DateTime? completedAtUtc = null,
        McpException? error = null)
    {
        return new McpExecutionSnapshot
        {
            ExecutionId = snapshot.ExecutionId,
            ToolId = snapshot.ToolId,
            ToolName = snapshot.ToolName,
            State = state ?? snapshot.State,
            Detail = detail ?? snapshot.Detail,
            CanCancel = canCancel ?? snapshot.CanCancel,
            StartedAtUtc = startedAtUtc ?? snapshot.StartedAtUtc,
            UpdatedAtUtc = updatedAtUtc ?? snapshot.UpdatedAtUtc,
            CompletedAtUtc = completedAtUtc ?? snapshot.CompletedAtUtc,
            Error = error ?? snapshot.Error
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        _workerTask.GetAwaiter().GetResult();
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

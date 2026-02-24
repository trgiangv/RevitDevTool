using System.Diagnostics;
using RevitDevTool.Bridge;
using RevitDevTool.Bridge.Abstractions;
using RevitDevTool.Bridge.Enums;
using RevitDevTool.Bridge.IPC;

namespace RevitDevTool.Console.Services;

public sealed class BatchOrchestrator
{
    private readonly RevitConnectionManager _mgr;

    public event Action<IHostInstance, PipeProgress>? OnProgress;

    public BatchOrchestrator(RevitConnectionManager mgr)
    {
        _mgr = mgr;
        _mgr.OnProgress += (inst, prog) => OnProgress?.Invoke(inst, prog);
    }

    public Task<BatchResult> ExecuteAsync(
        List<ResolvedJob> jobs, ProcessingMode mode, TimeSpan fileTimeout, CancellationToken ct)
    {
        return mode switch
        {
            ProcessingMode.SequentialSingle => ExecuteSequentialSingleAsync(jobs, fileTimeout, ct),
            ProcessingMode.SequentialMulti => ExecuteSequentialMultiAsync(jobs, fileTimeout, ct),
            ProcessingMode.Parallel => ExecuteParallelAsync(jobs, fileTimeout, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private async Task<BatchResult> ExecuteSequentialSingleAsync(
        List<ResolvedJob> jobs, TimeSpan fileTimeout, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var results = new List<JobResult>();
        var version = jobs[0].HostVersion;
        var instance = GetOrThrow(version);
        var shouldCloseHost = false;

        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested();
            var result = await ExecuteWithTimeoutAsync(instance, job, fileTimeout, ct).ConfigureAwait(false);
            results.Add(result);
            shouldCloseHost |= job.Lifecycle.CloseHost;
        }

        if (shouldCloseHost)
            await _mgr.ShutdownAsync(instance, ct).ConfigureAwait(false);

        return BuildResult(results, sw);
    }

    private async Task<BatchResult> ExecuteSequentialMultiAsync(
        List<ResolvedJob> jobs, TimeSpan fileTimeout, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var results = new List<JobResult>();
        var instancesToClose = new HashSet<string>();

        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested();
            var instance = GetOrThrow(job.HostVersion);
            var result = await ExecuteWithTimeoutAsync(instance, job, fileTimeout, ct).ConfigureAwait(false);
            results.Add(result);

            if (job.Lifecycle.CloseHost)
                instancesToClose.Add(instance.PipeName);
        }

        await ShutdownInstancesAsync(instancesToClose, ct).ConfigureAwait(false);

        return BuildResult(results, sw);
    }

    private async Task<BatchResult> ExecuteParallelAsync(
        List<ResolvedJob> jobs, TimeSpan fileTimeout, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var instancesToClose = new HashSet<string>();
        var tasks = new List<Task<JobResult>>();
        var jobInstanceMap = new List<(ResolvedJob Job, IHostInstance Instance)>();

        foreach (var group in jobs.GroupBy(j => j.HostVersion))
        {
            var instances = _mgr.GetConnectedInstances(group.Key);
            if (instances.Count == 0)
                throw new InvalidOperationException($"No connected instances for version {group.Key}.");

            var jobList = group.ToList();
            for (var i = 0; i < jobList.Count; i++)
            {
                var instance = instances[i % instances.Count];
                var job = jobList[i];
                tasks.Add(ExecuteWithTimeoutAsync(instance, job, fileTimeout, ct));
                jobInstanceMap.Add((job, instance));
            }
        }

        var results = (await Task.WhenAll(tasks).ConfigureAwait(false)).ToList();

        foreach (var (job, instance) in jobInstanceMap)
        {
            if (job.Lifecycle.CloseHost)
                instancesToClose.Add(instance.PipeName);
        }

        await ShutdownInstancesAsync(instancesToClose, ct).ConfigureAwait(false);

        return BuildResult(results, sw);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private async Task<JobResult> ExecuteWithTimeoutAsync(
        IHostInstance instance, ResolvedJob job, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            return await _mgr.ExecuteJobAsync(instance, job, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new JobResult
            {
                Success = false,
                Error = $"Job timed out after {timeout.TotalSeconds:F0}s: {job.FilePath}"
            };
        }
    }

    private IHostInstance GetOrThrow(string version)
    {
        var instances = _mgr.GetConnectedInstances(version);
        if (instances.Count == 0)
            throw new InvalidOperationException($"No connected instance for version {version}.");
        return instances[0];
    }

    private async Task ShutdownInstancesAsync(IEnumerable<string> pipeNames, CancellationToken ct)
    {
        foreach (var pipeName in pipeNames)
        {
            var inst = _mgr.GetAllConnectedInstances().FirstOrDefault(i => i.PipeName == pipeName);
            if (inst != null)
                await _mgr.ShutdownAsync(inst, ct).ConfigureAwait(false);
        }
    }

    private static BatchResult BuildResult(List<JobResult> results, Stopwatch sw)
    {
        sw.Stop();
        return new BatchResult
        {
            Results = results,
            TotalFiles = results.Count,
            SuccessCount = results.Count(r => r.Success),
            FailureCount = results.Count(r => !r.Success),
            TotalDurationMs = sw.ElapsedMilliseconds
        };
    }
}

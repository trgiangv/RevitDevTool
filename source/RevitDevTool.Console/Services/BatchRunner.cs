using System.Diagnostics;
using RevitDevTool.Bridge;
using RevitDevTool.Bridge.Abstractions;
using RevitDevTool.Bridge.Enums;
using RevitDevTool.Bridge.IPC;
using RevitDevTool.Console.Services.Hosting;

namespace RevitDevTool.Console.Services;

/// <summary>
/// Orchestrates batch execution with explicit connection modes:
/// attach (use existing host instances) or launch (start new ones).
/// Accepts a single <see cref="ExecutionPlan"/> that contains all resolved inputs.
/// </summary>
public sealed class BatchRunner : IAsyncDisposable
{
    private readonly RevitConnectionManager _mgr;
    private readonly IHostLauncher _launcher;

    public event Action<IHostInstance, PipeProgress>? OnProgress;
    public event Action<IHostInstance?, PipeLogEntry>? OnHostLog;
    public event Action<string>? OnDiagnostic;

    public BatchRunner(IHostDiscovery discovery, IHostLauncher launcher)
    {
        _mgr = new RevitConnectionManager(discovery);
        _launcher = launcher;
        _mgr.OnHostLog += (instance, log) => OnHostLog?.Invoke(instance, log);
    }

    public async Task<BatchResult> RunAsync(ExecutionPlan plan, CancellationToken ct)
    {
        var baselineRevitPids = SnapshotRunningRevitPids();

        if (plan.ConnectionMode == ConnectionMode.Launch)
            await LaunchAndConnectAsync(plan, ct).ConfigureAwait(false);
        else
            await DiscoverAndConnectAsync(ct).ConfigureAwait(false);

        var orchestrator = new BatchOrchestrator(_mgr);
        orchestrator.OnProgress += (inst, prog) => OnProgress?.Invoke(inst, prog);

        var fileTimeout = TimeSpan.FromSeconds(plan.TimeoutPerFileSeconds);
        var result = await orchestrator.ExecuteAsync(plan.Jobs, plan.ProcessingMode, fileTimeout, ct)
            .ConfigureAwait(false);
        ReportProcessHealth(plan, baselineRevitPids);
        return result;
    }

    public IReadOnlyList<IHostInstance> ConnectedInstances => _mgr.GetAllConnectedInstances();

    // ── Attach path ─────────────────────────────────────────────────

    private async Task DiscoverAndConnectAsync(CancellationToken ct)
    {
        await _mgr.DiscoverAndConnectAsync(ct).ConfigureAwait(false);

        if (_mgr.GetAllConnectedInstances().Count == 0)
            throw new InvalidOperationException(
                "No running host instances found with engine pipe. " +
                "Start the host application manually or use --launch to start automatically.");
    }

    // ── Launch path ─────────────────────────────────────────────────

    private async Task LaunchAndConnectAsync(ExecutionPlan plan, CancellationToken ct)
    {
        var requiredVersions = plan.Jobs.Select(j => j.HostVersion).Distinct();
        var timeout = TimeSpan.FromSeconds(plan.LaunchTimeoutSeconds);

        if (plan.ProcessingMode == ProcessingMode.Parallel)
        {
            foreach (var version in requiredVersions.Order())
            {
                for (var i = 0; i < plan.ParallelInstanceCount; i++)
                {
                    var inst = await _launcher.LaunchAsync(version, timeout, ct).ConfigureAwait(false);
                    await _mgr.ConnectAsync(inst, ct).ConfigureAwait(false);
                }
            }
        }
        else
        {
            var existing = _mgr.GetAllConnectedInstances();
            var launched = await _launcher.EnsureInstancesAsync(requiredVersions, existing, timeout, ct)
                .ConfigureAwait(false);
            foreach (var inst in launched)
                await _mgr.ConnectAsync(inst, ct).ConfigureAwait(false);
        }

        if (_mgr.GetAllConnectedInstances().Count == 0)
            throw new InvalidOperationException("Failed to launch any host instances.");
    }

    public async ValueTask DisposeAsync() => await _mgr.DisposeAsync().ConfigureAwait(false);

    private void ReportProcessHealth(ExecutionPlan plan, HashSet<int> baselineRevitPids)
    {
        var currentRevitPids = SnapshotRunningRevitPids();
        var managedPids = _mgr.GetAllConnectedInstances().Select(i => i.ProcessId).ToHashSet();
        var hasCloseHost = plan.Jobs.Any(j => j.Lifecycle.CloseHost);

        if (hasCloseHost)
        {
            var lingeringManaged = managedPids.Where(currentRevitPids.Contains).Order().ToList();
            foreach (var pid in lingeringManaged)
            {
                var inst = _mgr.GetAllConnectedInstances().FirstOrDefault(i => i.ProcessId == pid);
                var version = inst?.HostVersion ?? "unknown";
                if (RevitCrashWatcher.TryGetCrashSignal(pid, version, out var crashReason))
                    EmitDiagnostic($"[CrashRisk] {crashReason}");
                else
                    EmitDiagnostic($"[CrashRisk] Managed Revit PID {pid} still running after batch completion.");
            }
        }

        var unexpectedPids = currentRevitPids
            .Where(pid => !baselineRevitPids.Contains(pid) && !managedPids.Contains(pid))
            .Order()
            .ToList();

        foreach (var pid in unexpectedPids)
        {
            if (RevitCrashWatcher.TryGetCrashSignal(pid, "unknown", out var crashReason))
                EmitDiagnostic($"[CrashRisk] Unexpected Revit PID {pid}: {crashReason}");
            else
                EmitDiagnostic($"[CrashRisk] Unexpected Revit PID {pid} detected (not baseline, not managed).");
        }
    }

    private void EmitDiagnostic(string message)
    {
        System.Console.WriteLine(message);
        OnDiagnostic?.Invoke(message);
    }

    private static HashSet<int> SnapshotRunningRevitPids()
    {
        try
        {
            return Process.GetProcessesByName("Revit")
                .Select(p => p.Id)
                .ToHashSet();
        }
        catch
        {
            return [];
        }
    }
}

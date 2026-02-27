using RevitDevTool.Bridge;
using RevitDevTool.Console;
using RevitDevTool.Console.Services;
using RevitDevTool.Console.Services.Hosting;
using RevitDevTool.Desktop.Models;

namespace RevitDevTool.Desktop.Services;

public sealed class BatchExecutionService : IBatchExecutionService
{
    public event Action<HostProgressItem>? OnProgress;
    public event Action<HostLogItem>? OnHostLog;
    public event Action<string>? OnDiagnostic;

    public async Task<ExecutionPlan> LoadPlanAsync(
        string configPath,
        ProcessorRunOptions options,
        CancellationToken ct = default)
    {
        var config = await ConfigService.ParseConfigAsync(configPath, ct).ConfigureAwait(false);
        var overrides = new CliOverrides
        {
            ProcessingMode = options.ProcessingMode,
            ParallelInstanceCount = options.ParallelCount,
            Launch = options.ForceLaunch ? true : null
        };
        return ConfigService.BuildExecutionPlan(config, overrides);
    }

    public async Task<BatchResult> RunAsync(ExecutionPlan plan, CancellationToken ct = default)
    {
        var discovery = new RevitDiscovery();
        var launcher = new RevitLauncher();
        await using var runner = new BatchRunner(discovery, launcher);
        runner.OnProgress += HandleProgress;
        runner.OnHostLog += HandleHostLog;
        runner.OnDiagnostic += HandleDiagnostic;

        try
        {
            return await runner.RunAsync(plan, ct).ConfigureAwait(false);
        }
        finally
        {
            runner.OnProgress -= HandleProgress;
            runner.OnHostLog -= HandleHostLog;
            runner.OnDiagnostic -= HandleDiagnostic;
        }
    }

    public IReadOnlyList<HostInstanceItem> DiscoverInstances()
    {
        var discovery = new RevitDiscovery();
        return discovery.Discover()
            .Select(i => new HostInstanceItem(i.AppId, i.HostVersion, i.ProcessId, i.PipeName))
            .ToList();
    }

    private void HandleProgress(RevitDevTool.Bridge.Abstractions.IHostInstance instance, RevitDevTool.Bridge.IPC.PipeProgress progress)
    {
        var total = progress.Total <= 0 ? 1 : progress.Total;
        var percent = Math.Clamp(progress.Current / (double)total * 100.0d, 0.0d, 100.0d);
        var hostLabel = $"{instance.HostVersion}:{instance.ProcessId}";
        OnProgress?.Invoke(new HostProgressItem(hostLabel, progress.Message, progress.Current, progress.Total, percent));
    }

    private void HandleHostLog(RevitDevTool.Bridge.Abstractions.IHostInstance? instance, RevitDevTool.Bridge.IPC.PipeLogEntry log)
    {
        var hostLabel = instance == null ? "unknown" : $"{instance.HostVersion}:{instance.ProcessId}";
        var source = string.IsNullOrWhiteSpace(log.Source) ? hostLabel : $"{hostLabel}/{log.Source}";
        var ts = string.IsNullOrWhiteSpace(log.TimestampUtc) ? DateTimeOffset.UtcNow.ToString("O") : log.TimestampUtc;
        OnHostLog?.Invoke(new HostLogItem(ts, log.Level, source, log.Message, log.Exception));
    }

    private void HandleDiagnostic(string message)
    {
        OnDiagnostic?.Invoke(message);
    }
}

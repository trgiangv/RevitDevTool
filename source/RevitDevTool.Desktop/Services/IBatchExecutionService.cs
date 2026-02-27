using RevitDevTool.Bridge;
using RevitDevTool.Desktop.Models;

namespace RevitDevTool.Desktop.Services;

public interface IBatchExecutionService
{
    event Action<HostProgressItem>? OnProgress;
    event Action<HostLogItem>? OnHostLog;
    event Action<string>? OnDiagnostic;

    Task<ExecutionPlan> LoadPlanAsync(string configPath, ProcessorRunOptions options, CancellationToken ct = default);
    Task<BatchResult> RunAsync(ExecutionPlan plan, CancellationToken ct = default);
    IReadOnlyList<HostInstanceItem> DiscoverInstances();
}

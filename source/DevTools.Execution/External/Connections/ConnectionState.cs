using System.Collections.ObjectModel;
using System.Diagnostics;
using DevTools.Utilities;
namespace DevTools.Execution.External.Connections;

public sealed partial class ConnectionState : ObservableObject
{
    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial string Endpoint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int QueueDepth { get; set; }

    [ObservableProperty]
    public partial int TotalToolCalls { get; set; }

    [ObservableProperty]
    public partial bool IsExecuting { get; set; }

    [ObservableProperty]
    public partial string CurrentToolName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime ExecutionStartedAtUtc { get; private set; }
    public ObservableCollection<ToolCallMetric> ToolCalls { get; } = [];

    public void SetEndpoint(string endpoint)
    {
        UpdateUiState(() => Endpoint = endpoint);
    }

    public void SetConnectedState(int connectedClients)
    {
        UpdateUiState(() => IsConnected = connectedClients > 0);
    }

    public void SetQueueDepth(int depth)
    {
        UpdateUiState(() => QueueDepth = Math.Max(0, depth));
    }

    public ExecutionScope BeginExecution(string toolName)
    {
        UpdateUiState(() =>
        {
            IsExecuting = true;
            CurrentToolName = toolName;
            CurrentStatusMessage = $"Queued '{toolName}'...";
            ExecutionStartedAtUtc = DateTime.UtcNow;
        });
        return new ExecutionScope(this, toolName);
    }

    internal void UpdateExecution(string toolName, string statusMessage)
    {
        UpdateUiState(() =>
        {
            CurrentToolName = toolName;
            CurrentStatusMessage = statusMessage;
        });
    }

    internal void ResetExecution()
    {
        UpdateUiState(() =>
        {
            IsExecuting = false;
            CurrentToolName = string.Empty;
            CurrentStatusMessage = string.Empty;
            ExecutionStartedAtUtc = default;
        });
    }

    public void RecordCall(string toolId, string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolId) || string.IsNullOrWhiteSpace(toolName))
            return;

        UpdateUiState(() =>
        {
            TotalToolCalls++;
            var existing = ToolCalls.FirstOrDefault(item =>
                item.ToolId.Equals(toolId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.ToolName = toolName;
                existing.Count++;
                return;
            }

            ToolCalls.Add(new ToolCallMetric(toolId, toolName, 1));
        });
    }

    private static void UpdateUiState(Action updateAction)
    {
        HostUiHelper.RunOnMainThread(updateAction);
    }
}

public sealed class ExecutionScope : IDisposable
{
    private readonly ConnectionState _state;
    private readonly string _toolName;
    private readonly Stopwatch _stopwatch;
    private bool _completed;

    internal ExecutionScope(ConnectionState state, string toolName)
    {
        _state = state;
        _toolName = toolName;
        _stopwatch = Stopwatch.StartNew();
    }

    public void MarkRunning()
    {
        _state.UpdateExecution(_toolName, $"Running '{_toolName}'...");
    }

    public void Complete(McpToolExecutionResult result)
    {
        if (_completed) return;
        _completed = true;
        _stopwatch.Stop();

        var elapsed = _stopwatch.Elapsed;
        var detail = !string.IsNullOrWhiteSpace(result.Detail)
            ? result.Detail
            : result.Error?.Message ?? string.Empty;

        var traceMessage = $"[MCP] Tool '{_toolName}' {result.State} in {elapsed.TotalSeconds:F1}s. {detail}";
        if (result.State == ExecutionState.Completed)
            Trace.TraceInformation(traceMessage);
        else
            Trace.TraceWarning(traceMessage);

        _state.ResetExecution();
    }

    public void Dispose()
    {
        if (_completed) return;
        _stopwatch.Stop();

        Trace.TraceWarning(
            $"[MCP] Tool '{_toolName}' scope disposed without completion after {_stopwatch.Elapsed.TotalSeconds:F1}s.");
        _state.ResetExecution();
    }
}

using System.Collections.ObjectModel;
using System.Diagnostics;
using RevitDevTool.Contracts;
using RevitDevTool.Utils;
namespace RevitDevTool.Mcp.Models;

public sealed partial class McpBridgeState : ObservableObject
{
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _endpoint = string.Empty;
    [ObservableProperty] private int _queueDepth;
    [ObservableProperty] private int _totalToolCalls;
    [ObservableProperty] private bool _isExecuting;
    [ObservableProperty] private string _currentToolName = string.Empty;
    [ObservableProperty] private string _currentStage = string.Empty;
    [ObservableProperty] private string _currentStatusMessage = string.Empty;

    public ObservableCollection<McpToolCallMetric> ToolCalls { get; } = [];

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

    public void RecordQueued(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return;

        UpdateUiState(() =>
        {
            IsExecuting = true;
            CurrentToolName = toolName;
            CurrentStage = ExecutionState.Queued.ToString().ToLowerInvariant();
            CurrentStatusMessage = $"Queued '{toolName}'...";
        });
    }

    public void StartExecution(string toolName, string detail)
    {
        UpdateUiState(() =>
        {
            IsExecuting = true;
            CurrentToolName = toolName;
            CurrentStage = nameof(ExecutionState.Preparing).ToLowerInvariant();
            CurrentStatusMessage = detail;
        });
    }

    public void ReportProgress(McpProgressUpdate progress)
    {
        UpdateUiState(() =>
        {
            CurrentStage = progress.State.ToString().ToLowerInvariant();
            CurrentStatusMessage = progress.Detail;
        });
    }

    public void CompleteExecution(string toolName, McpToolExecutionResult result)
    {
        UpdateUiState(() =>
        {
            IsExecuting = false;
            CurrentToolName = string.Empty;
            CurrentStage = string.Empty;
            CurrentStatusMessage = string.Empty;
        });

        var detail = !string.IsNullOrWhiteSpace(result.Detail)
            ? result.Detail
            : result.Error?.Message ?? string.Empty;
        var traceMessage = $"[MCP] Tool '{toolName}' completed. State={result.State}. Detail={detail}";

        if (result.State == ExecutionState.Completed)
        {
            Trace.TraceInformation(traceMessage);
            return;
        }

        Trace.TraceWarning(traceMessage);
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

            ToolCalls.Add(new McpToolCallMetric(toolId, toolName, 1));
        });
    }

    private static void UpdateUiState(Action updateAction)
    {
        DispatcherHelper.RunOnMainThread(updateAction);
    }
}
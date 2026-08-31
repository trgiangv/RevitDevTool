using System.Collections.ObjectModel;
using System.Diagnostics;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Results;
using DevTools.Mcp.Core.Sessions;
using DevTools.UI;
using Microsoft.Extensions.Logging;
using ZLogger;
namespace DevTools.Execution.External.Connections;

public sealed partial class ConnectionState(ILogger<ConnectionState> logger)
    : ObservableObject, IMcpPipeConnectionTracker
{
    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial string Endpoint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string McpEndpoint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int McpClientCount { get; set; }

    public bool McpIsConnected => McpClientCount > 0;

    public bool McpIsListening => !string.IsNullOrWhiteSpace(McpEndpoint);

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

    public void SetMcpEndpoint(string endpoint)
    {
        UpdateUiState(() =>
        {
            McpEndpoint = endpoint;
            OnPropertyChanged(nameof(McpIsListening));
            OnPropertyChanged(nameof(McpIsConnected));
        });
    }

    public void SetMcpClientCount(int clientCount)
    {
        UpdateUiState(() =>
        {
            McpClientCount = Math.Max(0, clientCount);
            OnPropertyChanged(nameof(McpIsConnected));
        });
    }

    public void ClearMcpState()
    {
        UpdateUiState(() =>
        {
            McpEndpoint = string.Empty;
            McpClientCount = 0;
            OnPropertyChanged(nameof(McpIsListening));
            OnPropertyChanged(nameof(McpIsConnected));
        });
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
        return new ExecutionScope(this, toolName, logger);
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
    private readonly ILogger? _logger;
    private readonly Stopwatch _stopwatch;
    private bool _completed;

    internal ExecutionScope(ConnectionState state, string toolName, ILogger? logger = null)
    {
        _state = state;
        _toolName = toolName;
        _logger = logger;
        _stopwatch = Stopwatch.StartNew();
    }

    public void MarkRunning()
    {
        _state.UpdateExecution(_toolName, $"Running '{_toolName}'...");
    }

    public void Complete(McpInvocation invocation, McpResult<McpInvocationResponse> result, string detail)
    {
        if (_completed) return;
        _completed = true;
        _stopwatch.Stop();

        var elapsed = _stopwatch.Elapsed;
        var message = !string.IsNullOrWhiteSpace(detail) ? detail : result.Error?.Message ?? string.Empty;
        var traceMessage = $"Tool '{_toolName}' {invocation.ExecutionState} in {elapsed.TotalSeconds:F1}s. {message}";
        if (invocation.ExecutionState == ExecutionState.Completed)
            _logger?.ZLogInformation($"{traceMessage}");
        else
            _logger?.ZLogWarning($"{traceMessage}");

        _state.ResetExecution();
    }

    public void Dispose()
    {
        if (_completed) return;
        _stopwatch.Stop();

        _logger?.ZLogWarning(
            $"Tool '{_toolName}' scope disposed without completion after {_stopwatch.Elapsed.TotalSeconds:F1}s.");
        _state.ResetExecution();
    }
}

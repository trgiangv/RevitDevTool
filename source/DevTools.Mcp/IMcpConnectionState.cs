namespace DevTools.Mcp;

public interface IMcpConnectionState
{
    bool IsConnected { get; }
    string? PipeEndpointName { get; }
    string? CurrentToolName { get; }
    int QueueDepth { get; }
    event Action? StateChanged;
}

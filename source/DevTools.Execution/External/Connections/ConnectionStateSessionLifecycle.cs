using DevTools.Ipc;
using DevTools.Logging;
using DevTools.Mcp.Dispatch;

namespace DevTools.Execution.External.Connections;

/// <summary>
/// Updates <see cref="ConnectionState"/> when MCP client sessions connect or disconnect.
/// </summary>
public sealed class ConnectionStateSessionLifecycle(
    ConnectionState state,
    IHostAppInfo hostInfo) : IMcpSessionLifecycle
{
    private int _connectedClients;

    public void OnSessionAccepted()
    {
        var count = Interlocked.Increment(ref _connectedClients);
        state.SetConnectedState(count);
        if (count == 1)
        {
            state.SetEndpoint(HostPipeName.Format(
                hostInfo.Host.ToString(),
                hostInfo.VersionNumber,
                hostInfo.ProcessId));
        }
    }

    public void OnSessionEnded()
    {
        var count = Interlocked.Decrement(ref _connectedClients);
        if (count < 0)
        {
            Interlocked.Exchange(ref _connectedClients, 0);
            count = 0;
        }

        state.SetConnectedState(count);
    }
}

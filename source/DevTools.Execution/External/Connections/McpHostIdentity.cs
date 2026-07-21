using DevTools.Logging;

namespace DevTools.Execution.External.Connections;

internal sealed class McpHostIdentity(IHostAppInfo hostInfo) : IMcpHostIdentity
{
    public string HostName { get; } = hostInfo.Host.ToString();
}

using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Mcp.Contracts;

public interface IMachineLister
{
    Task<CallToolResult> ListAsync(CancellationToken cancellationToken = default);
}

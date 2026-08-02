using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Server.Contracts;

public interface IMachineLister
{
    Task<CallToolResult> ListAsync(CancellationToken cancellationToken = default);
}

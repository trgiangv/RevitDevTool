using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Protocol;

namespace DevTools.Mcp.Core;

/// <summary>Dispatches registered MCP primitives to the execution backend.</summary>
public interface IMcpPrimitiveDispatcher
{
    Task<McpResult<McpInvocationResponse>> DispatchToolAsync(
        McpRegisteredTool tool,
        McpInvocationRequest request,
        IHostContextExecutor hostContext,
        CancellationToken ct = default);

    McpReadResourceResponse ReadResource(
        McpRegisteredResource resource,
        string uri,
        CancellationToken ct = default);

    void ClearCaches();
}

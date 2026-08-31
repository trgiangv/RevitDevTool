using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Results;
using ModelContextProtocol.Protocol;
namespace DevTools.Mcp.Core.Invocation;

/// <summary>Dispatches registered MCP primitives to the execution backend.</summary>
public interface IMcpPrimitiveDispatcher
{
    Task<McpResult<McpInvocationResponse>> DispatchToolAsync(
        McpRegisteredTool tool,
        CallToolRequestParams request,
        IHostContextExecutor hostContext,
        CancellationToken ct = default);

    ReadResourceResult ReadResource(
        McpRegisteredResource resource,
        string uri,
        CancellationToken ct = default);

    void ClearCaches();
}

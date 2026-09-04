using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Results;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Invocation;

/// <summary>Executes one MCP primitive source kind behind the host router.</summary>
public interface IMcpPrimitiveBackend
{
    ExecutionMode SourceKind { get; }

    Task<McpResult<McpInvocationResponse>> InvokeToolAsync(
        McpRegisteredTool tool,
        CallToolRequestParams request,
        IHostContextExecutor hostContext,
        CancellationToken cancellationToken);

    ReadResourceResult ReadResource(
        McpRegisteredResource resource,
        string uri,
        CancellationToken cancellationToken);

    void ClearCaches();
}

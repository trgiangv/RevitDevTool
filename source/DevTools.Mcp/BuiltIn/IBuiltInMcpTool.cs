using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.BuiltIn;

/// <summary>Self-describing built-in MCP tool, registered via DI.</summary>
public interface IBuiltInMcpTool
{
    string Name { get; }
    Tool ProtocolTool { get; }
    Task<McpToolExecutionResult> ExecuteAsync(string payloadJson, CancellationToken ct);
}

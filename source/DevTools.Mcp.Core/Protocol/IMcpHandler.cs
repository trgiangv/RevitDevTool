using System.Text.Json.Nodes;

namespace DevTools.Mcp.Core.Protocol;

/// <summary>
/// Host named-pipe MCP JSON-RPC handler (no SDK). Replaces SDK <c>McpServer</c> on the host pipe.
/// </summary>
public interface IMcpHandler
{
    /// <summary>
    /// Handles one JSON-RPC request object and returns a response object (or notification: null).
    /// </summary>
    ValueTask<JsonObject?> HandleAsync(JsonObject request, CancellationToken cancellationToken = default);
}

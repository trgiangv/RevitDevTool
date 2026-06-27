using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Dispatch;

/// <summary>
/// Dispatches MCP primitive invocations (tool calls, prompt gets, resource reads)
/// to the appropriate execution backend (dotnet assembly, Python, built-in C#).
/// Implementations live in the host Execution layer; the interface is defined here
/// so that <see cref="Handlers.McpBridgeRequestHandler"/> can depend on it without
/// creating a circular project reference.
/// </summary>
public interface IMcpPrimitiveDispatcher
{
    Task<McpToolExecutionResult> DispatchToolAsync(
        McpRegisteredTool tool,
        string? payloadJson,
        IHostContextExecutor hostContext,
        CancellationToken ct = default);

    GetPromptResult GetPrompt(
        McpRegisteredPrompt prompt,
        IReadOnlyDictionary<string, JsonElement>? arguments,
        CancellationToken ct = default);

    ReadResourceResult ReadResource(
        McpRegisteredResource resource,
        string uri,
        CancellationToken ct = default);

    void ClearCaches();
}

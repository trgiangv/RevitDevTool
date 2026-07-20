using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Routing;

public interface IHostMcpSession : IAsyncDisposable
{
    HostInstanceDescriptor Instance { get; }
    int Generation { get; }
    bool IsConnected { get; }
    Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct);
    Task<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken ct);
    Task<IList<McpClientResource>> ListResourcesAsync(CancellationToken ct);
    Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct);
    Task<CallToolResult> CallToolAsync(
        string name,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken ct);
    Task<GetPromptResult> GetPromptAsync(
        string name,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken ct);
    Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct);
    event Action? CatalogChanged;
    event Action? Disconnected;
}

using DevTools.Mcp.Core.Protocol;

namespace DevTools.Mcp.Adapter.Host;

/// <summary>Configuration for <see cref="McpHandler"/>.</summary>
public sealed class McpHandlerOptions
{
    public string ServerName { get; init; } = "DevTools.Host";

    public string ServerVersion { get; init; } = "1.0.0";
}

using ModelContextProtocol.Server;

namespace DevTools.Mcp.BuiltIn;

/// <summary>Typed SDK-bound built-in MCP resource, registered via DI.</summary>
public interface IBuiltInMcpResource
{
    McpServerResource Primitive { get; }
}

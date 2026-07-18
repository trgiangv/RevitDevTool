using ModelContextProtocol.Server;

namespace DevTools.Mcp.BuiltIn;

/// <summary>Typed SDK-bound built-in MCP tool, registered via DI.</summary>
public interface IBuiltInMcpTool
{
    McpServerTool Primitive { get; }
}

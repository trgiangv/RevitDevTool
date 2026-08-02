using ModelContextProtocol.Server;

namespace DevTools.Mcp.Catalog;

/// <summary>
/// Host built-in MCP tool registered via DI.
/// Schema and invoke come from SDK <see cref="McpServerTool.Create"/> — no hand-written InputSchema.
/// </summary>
public interface IBuiltInMcpTool
{
    string Name { get; }
    McpServerTool ServerTool { get; }
}

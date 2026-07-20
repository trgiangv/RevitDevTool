using ModelContextProtocol.Server;

namespace DevTools.Mcp.BuiltIn;

/// <summary>Typed SDK-bound built-in MCP prompt, registered via DI.</summary>
public interface IBuiltInMcpPrompt
{
    McpServerPrompt Primitive { get; }
}

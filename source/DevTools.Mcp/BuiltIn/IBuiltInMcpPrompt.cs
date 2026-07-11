using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.BuiltIn;

/// <summary>Self-describing built-in MCP prompt, registered via DI.</summary>
public interface IBuiltInMcpPrompt
{
    Prompt ProtocolPrompt { get; }
    GetPromptResult Get(IReadOnlyDictionary<string, JsonElement>? arguments);
}

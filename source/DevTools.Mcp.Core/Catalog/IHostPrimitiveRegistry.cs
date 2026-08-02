namespace DevTools.Mcp.Core;

/// <summary>Read-only registry of MCP primitives loaded in the host process.</summary>
public interface IHostPrimitiveRegistry
{
    IReadOnlyList<McpRegisteredTool> RegisteredTools { get; }
    IReadOnlyList<McpRegisteredResource> ResourceCatalog { get; }

    IReadOnlyList<McpRegisteredTool> EnsureLoaded();
    bool TryGetTool(string? toolId, string? toolName, out McpRegisteredTool? tool);
    bool TryResolveResourceByUri(string uri, out McpRegisteredResource? resource);
}

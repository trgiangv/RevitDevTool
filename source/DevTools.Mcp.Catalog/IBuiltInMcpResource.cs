using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog;

/// <summary>Self-describing built-in MCP resource, registered via DI.</summary>
public interface IBuiltInMcpResource
{
    string UriTemplate { get; }
    Resource ProtocolResource { get; }
    ReadResourceResult Read(string uri);
}

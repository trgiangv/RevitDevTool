namespace DevTools.Mcp.Dispatch;

/// <summary>Supplies the host product name for MCP dispatch metadata logging.</summary>
public interface IMcpHostIdentity
{
    string HostName { get; }
}

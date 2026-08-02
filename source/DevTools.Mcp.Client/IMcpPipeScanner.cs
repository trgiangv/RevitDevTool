namespace DevTools.Mcp.Client;

/// <summary>Enumerates local DevToolsMcp_* named pipes without connecting to them.</summary>
public interface IMcpPipeScanner
{
    IReadOnlyCollection<string> Discover();
}

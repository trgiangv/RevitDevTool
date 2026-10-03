namespace DevTools.Mcp.Core.Sessions;

/// <summary>Listen endpoint and client count for one named-pipe server.</summary>
public interface IConnectTracker
{
    void SetEndpoint(string endpoint);

    void SetClientCount(int count);

    void Reset();
}

namespace DevTools.Mcp.Client;

/// <summary>Background poll that opens sessions for new pipes and disposes sessions for pipes that disappear.</summary>
public interface IHostDiscovery
{
    Task RunAsync(CancellationToken ct);
}

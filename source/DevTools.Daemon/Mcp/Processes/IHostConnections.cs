namespace DevTools.Daemon.Mcp.Processes;

public interface IHostConnections
{
    ProcessCatalogs Catalog { get; }
    IProcessSession? GetByProcessId(int processId);
    Task RunAsync(CancellationToken ct);
    event Action? Changed;
}

namespace DevTools.Mcp;

public interface IHostBridgeClient : IAsyncDisposable
{
    InstanceInfo Info { get; }
    string PipeName { get; }
    bool IsConnected { get; }
    Task<BridgeMessage> RequestAsync(string method, object? @params = null, CancellationToken ct = default);
}

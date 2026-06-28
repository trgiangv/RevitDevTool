using System.Text.Json;

namespace DevTools.Mcp;

public interface IHostBridgeClient : IAsyncDisposable
{
    InstanceInfo Info { get; }
    string PipeName { get; }
    bool IsConnected { get; }
    Task<BridgeMessage> RequestAsync(string method, JsonElement? @params = null, CancellationToken ct = default);
}
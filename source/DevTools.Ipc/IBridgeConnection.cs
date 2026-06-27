namespace DevTools.Ipc;

public interface IBridgeConnection : IDisposable
{
    Task SendAsync(BridgeMessage message, CancellationToken ct = default);
    event Action<BridgeMessage>? MessageReceived;
    event Action? Disconnected;
    bool IsConnected { get; }
}

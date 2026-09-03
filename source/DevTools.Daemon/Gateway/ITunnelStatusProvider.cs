namespace DevTools.Daemon.Gateway;

public enum TunnelStatus
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
}

public sealed class TunnelStatusChangedArgs(TunnelStatus status) : EventArgs
{
    public TunnelStatus Status { get; } = status;
}

public interface ITunnelStatusProvider
{
    TunnelStatus Status { get; }
    event EventHandler<TunnelStatusChangedArgs>? StatusChanged;
}

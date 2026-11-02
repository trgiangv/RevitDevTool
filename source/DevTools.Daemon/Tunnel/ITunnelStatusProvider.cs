namespace DevTools.Daemon.Tunnel;

public enum TunnelStatus
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Incompatible,
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

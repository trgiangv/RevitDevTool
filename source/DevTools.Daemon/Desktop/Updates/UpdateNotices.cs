using DevTools.Update.Engine;

namespace DevTools.Daemon.Desktop.Updates;

/// <summary>
///     Receives the engine notices and sends them to the tray. It has no dependencies, so the engine and the tray do not form a cycle.
/// </summary>
public sealed class UpdateNotices : IUpdateNotifier
{
    public event Action<UpdateNotice>? Raised;

    public event Action? ShutdownRequested;

    public void Notify(UpdateNotice notice) => Raised?.Invoke(notice);

    public void RequestShutdownForUpdate() => ShutdownRequested?.Invoke();
}

using DevTools.Update.Autodesk;

namespace DevTools.Update.Engine;

/// <summary>
///     Shows engine events to the user. The Daemon implements it.
/// </summary>
public interface IUpdateNotifier
{
    void Notify(UpdateNotice notice);

    /// <summary>
    ///     Stops the Daemon. Velopack then swaps <c>current\</c> and starts the Daemon again.
    /// </summary>
    void RequestShutdownForUpdate();
}

public enum UpdateNoticeKind
{
    Available,
    Applied,
    Pending,
    DaemonPending,
    NewTarget,
    Failed
}

/// <summary>
///     One notification (0050). <see cref="Targets"/> is set for <see cref="UpdateNoticeKind.NewTarget"/>.
/// </summary>
public sealed record UpdateNotice(
    UpdateNoticeKind Kind,
    string Title,
    string Message,
    string? Version = null,
    string? NotesUrl = null,
    IReadOnlyList<AddinTarget>? Targets = null);

internal sealed class NullUpdateNotifier : IUpdateNotifier
{
    public void Notify(UpdateNotice notice)
    {
    }

    public void RequestShutdownForUpdate()
    {
    }
}

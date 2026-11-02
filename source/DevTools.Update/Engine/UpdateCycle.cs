namespace DevTools.Update.Engine;

public enum UpdateTrigger
{
    Scheduled,
    Manual,
    UserApproved
}

public enum UpdateCycleStatus
{
    NotConfigured,
    Disabled,
    NoRelease,
    UpToDate,
    Notified,
    Updated
}

public sealed record UpdateCycleResult(
    UpdateCycleStatus Status,
    string? Version = null,
    IReadOnlyList<string>? Applied = null,
    IReadOnlyList<string>? Pending = null);

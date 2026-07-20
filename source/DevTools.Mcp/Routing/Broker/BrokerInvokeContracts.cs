namespace DevTools.Mcp.Routing.Broker;

public sealed record BrokerInvokeCandidate(int HostId, string HostApp, string VersionNumber);

public sealed record BrokerInvokePayload(
    string Status,
    string Target,
    int? RequestedHostId,
    IReadOnlyList<BrokerInvokeCandidate> Candidates,
    bool MayHaveExecuted = false,
    string? ErrorCode = null);

public static class BrokerInvokeStatus
{
    public const string HostSelectionRequired = "host_selection_required";
    public const string HostMismatch = "host_mismatch";
    public const string TargetNotFound = "target_not_found";
    public const string HostDisconnected = "host_disconnected";
    public const string ConnectionLost = "connection_lost";
    public const string TimedOut = "timed_out";
    public const string HostFailed = "host_failed";
}

using DevTools.Update.Autodesk;
using System.Text.Json.Serialization;

namespace DevTools.Update.Installations;

/// <summary>
///     <c>installation.json</c>: what the user selected, what is installed, and the update policy (0047, 0050).
/// </summary>
public sealed class Installation
{
    public int Schema { get; set; } = 1;
    public UpdatePolicy Policy { get; set; } = new();
    public List<AddinTarget> Targets { get; set; } = [];
    public List<InstalledPayload> Payloads { get; set; } = [];
    public List<AddinTarget> KnownTargets { get; set; } = [];

    public IEnumerable<string> SelectedHostVersions() =>
        Targets.Select(target => target.HostVersion).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    public string? InstalledVersion(string hostVersion) => Payloads.Find(entry => entry.HostVersion == hostVersion)?.Version;

    public InstalledPayload Payload(string hostVersion)
    {
        var payload = Payloads.Find(entry => entry.HostVersion == hostVersion);
        if (payload is not null) return payload;

        payload = new InstalledPayload { HostVersion = hostVersion };
        Payloads.Add(payload);
        return payload;
    }
}

public sealed class UpdatePolicy
{
    public const string StableChannel = "stable";
    public const string PrereleaseChannel = "prerelease";
    public const int MinimumIntervalMinutes = 15;

    public UpdateMode Mode { get; set; } = UpdateMode.Auto;
    public string Channel { get; set; } = StableChannel;
    public int IntervalMinutes { get; set; } = 60;
    public string? SkippedVersion { get; set; }

    [JsonIgnore]
    public bool IsPrerelease => Channel == PrereleaseChannel;

    [JsonIgnore]
    public TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(IntervalMinutes, MinimumIntervalMinutes));
}

public enum UpdateMode
{
    [JsonStringEnumMemberName("auto")] Auto,
    [JsonStringEnumMemberName("notify")] Notify,
    [JsonStringEnumMemberName("off")] Off
}

/// <summary>
///     One payload folder: the installed version, the version that waits for its lockers to stop, and the rollback version.
/// </summary>
public sealed class InstalledPayload
{
    public string HostVersion { get; set; } = "";
    public string? Version { get; set; }
    public string? PendingVersion { get; set; }
    public string? RollbackVersion { get; set; }
}

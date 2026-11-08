using Build.Trains;
using System.Text.RegularExpressions;

namespace Build.Releases;

public sealed record ReleaseInput(
    Train Train,
    string Version,
    string? Previous,
    string? Changelog,
    string? MinUpdaterVersion = null,
    IReadOnlyList<string>? UnshippedApi = null,
    IReadOnlyList<string>? DaemonReleases = null);

public static partial class ReleaseRules
{
    public const string MinUpdaterProperty = "MinUpdaterVersion";
    public const string UnshippedApiFile = "source/DevTools.Testing.Abstractions/PublicAPI.Unshipped.txt";

    public static IReadOnlyList<string> Refusals(ReleaseInput input)
    {
        var train = input.Train;
        if (input.Version.StartsWith("0.0.0", StringComparison.Ordinal))
        {
            return [$"Version {input.Version} is for local and CI builds only; set the release version in {train.VersionFile}."];
        }

        var version = ReleaseVersion.Parse(input.Version, train.Grammar);
        if (version is null)
        {
            return [$"Version '{input.Version}' does not match {ReleaseVersion.Describe(train.Grammar)}; fix it in {train.VersionFile}."];
        }

        var refusals = new List<string>();
        var previous = input.Previous is null ? null : ReleaseVersion.Parse(input.Previous, train.Grammar);
        if (previous is not null && version.IsPrerelease && previous.IsPrerelease && version.Core == previous.Core && version.Label < previous.Label)
        {
            refusals.Add($"Version {input.Version} goes back from {previous.Label.ToString()!.ToLowerInvariant()} to {version.Label.ToString()!.ToLowerInvariant()}; keep the label or raise the version in {train.VersionFile}.");
        }
        else if (previous is not null && version.CompareTo(previous) <= 0)
        {
            refusals.Add($"Version {input.Version} is not greater than the latest tag {train.Tag(input.Previous!)}; raise it in {train.VersionFile}.");
        }

        if (!version.IsPrerelease && !HasSection(input.Changelog, input.Version))
        {
            refusals.Add($"Stable version {input.Version} needs a '## [{input.Version}]' section in {train.Changelog}.");
        }

        var minUpdater = input.MinUpdaterVersion is null ? null : ReleaseVersion.Parse(input.MinUpdaterVersion, train.Grammar);
        if (train == Train.Addin && minUpdater is not null && !HasDaemonFor(minUpdater, version.IsPrerelease, input.DaemonReleases ?? []))
        {
            refusals.Add(version.IsPrerelease
                ? $"Add-in {input.Version} needs a daemon release at or above {MinUpdaterProperty} {input.MinUpdaterVersion}; release daemon first."
                : $"Stable add-in {input.Version} needs a stable daemon release at or above {MinUpdaterProperty} {input.MinUpdaterVersion}; release daemon first.");
        }

        if (train == Train.Addin && !version.IsPrerelease)
        {
            if (minUpdater is null || minUpdater.IsPrerelease)
            {
                refusals.Add($"Stable add-in {input.Version} needs a stable {MinUpdaterProperty} in {train.VersionFile}, not '{input.MinUpdaterVersion}'.");
            }

            if (input.UnshippedApi?.Any(IsApiLine) == true)
            {
                refusals.Add($"Stable add-in {input.Version} needs an empty {UnshippedApiFile}; move its lines to PublicAPI.Shipped.txt.");
            }
        }

        return refusals;
    }

    public static bool HasSection(string? changelog, string version) =>
        changelog is not null && Regex.IsMatch(changelog, $@"^## \[{Regex.Escape(version)}\]", RegexOptions.Multiline);

    public static bool IsBreaking(string message) => BreakingRegex().IsMatch(message);

    public static bool IsFeature(string message) => FeatureRegex().IsMatch(message);

    public static bool IsContract(string path, string content) =>
        Path.GetFileName(path) is "TestingRuntime.props" or "constants.py" ||
        (path.EndsWith(".cs", StringComparison.Ordinal) && ContractRegex().IsMatch(content));

    private static bool HasDaemonFor(ReleaseVersion minUpdater, bool prerelease, IReadOnlyList<string> daemonReleases) =>
        daemonReleases
            .Select(release => ReleaseVersion.Parse(release, Train.Daemon.Grammar))
            .Any(daemon => daemon is not null && daemon.CompareTo(minUpdater) >= 0 && (prerelease || !daemon.IsPrerelease));

    private static bool IsApiLine(string line) =>
        !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#nullable", StringComparison.Ordinal);

    [GeneratedRegex(@"\A\w+(\([^)]*\))?!:|^BREAKING[ -]CHANGE:", RegexOptions.Multiline)]
    private static partial Regex BreakingRegex();

    [GeneratedRegex(@"\Afeat(\([^)]*\))?!?:")]
    private static partial Regex FeatureRegex();

    [GeneratedRegex(@"new\s+ProtocolRange\s*\(|\bProtocolRange\s+\w+\s*(\{\s*get;\s*\}\s*)?=\s*new\s*\(")]
    private static partial Regex ContractRegex();
}

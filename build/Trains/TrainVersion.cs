namespace Build.Trains;

public sealed record TrainVersion(Train Train, string Version, bool IsPrerelease, string? Previous)
{
    public string Tag => Train.Tag(Version);

    public string? PreviousTag => Previous is null ? null : Train.Tag(Previous);

    public bool IsLatest => Train == Train.Daemon && !IsPrerelease;

    public static string NonRelease(Train train, string? runNumber, DateTimeOffset now)
    {
        var stamp = now.UtcDateTime.ToString("yyyyMMddHHmmss");
        var hasRun = !string.IsNullOrWhiteSpace(runNumber);
        return train.Grammar == VersionGrammar.Pep440
            ? $"0.0.0.dev{(hasRun ? runNumber : stamp)}"
            : hasRun ? $"0.0.0-ci.{runNumber}" : $"0.0.0-local.{stamp}";
    }

    public static string? PickPrevious(Train train, IEnumerable<string> tags) =>
        tags.Where(tag => tag.StartsWith(train.TagPrefix, StringComparison.Ordinal))
            .Select(tag => tag[train.TagPrefix.Length..])
            .Select(version => (Text: version, Parsed: ReleaseVersion.Parse(version, train.Grammar)))
            .Where(item => item.Parsed is not null)
            .OrderByDescending(item => item.Parsed)
            .Select(item => item.Text)
            .FirstOrDefault();

    /// <summary>
    ///     The newest release below <paramref name="version"/> with the same prerelease state, so it shares the Velopack channel.
    /// </summary>
    public static string? PickSameChannel(Train train, IEnumerable<string> tags, string version)
    {
        var current = ReleaseVersion.Parse(version, train.Grammar);
        return current is null
            ? null
            : PickPrevious(train, tags.Where(tag =>
                tag.StartsWith(train.TagPrefix, StringComparison.Ordinal) &&
                ReleaseVersion.Parse(tag[train.TagPrefix.Length..], train.Grammar) is { } parsed &&
                parsed.IsPrerelease == current.IsPrerelease &&
                parsed.CompareTo(current) < 0));
    }
}

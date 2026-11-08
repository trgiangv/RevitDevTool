namespace Build.Trains;

public sealed record Commit(string Message, IReadOnlyList<string> Files);

public static class BumpWarnings
{
    public static IReadOnlyList<string> Collect(string root, Train train, string? baseSha)
    {
        if (string.IsNullOrWhiteSpace(baseSha) || baseSha.All(character => character == '0')) return [];
        if (!Git.Succeeds(root, "cat-file", "-e", $"{baseSha}^{{commit}}"))
        {
            return [$"Base {baseSha} is not in this clone; fetch the full history to get the bump warnings."];
        }

        var paths = TrainPaths.Compute(root, train);
        var changed = Git.Lines(root, "diff", "--name-only", baseSha, "HEAD");
        var commits = Commits(root, $"{baseSha}..HEAD");
        var version = ReleaseVersion.Parse(VersionFile.Read(root, train.VersionFile), train.Grammar);
        var tags = Git.Tags(root, train);
        var previous = TrainVersion.PickPrevious(train, tags);

        var warnings = new List<string>();
        if (changed.Any(path => PathGlob.IsMatchAny(paths, path)) && !changed.Contains(train.VersionFile, StringComparer.Ordinal))
        {
            warnings.Add($"Train {train.Name} changed but {train.VersionFile} did not; bump it before the next {train.Name} release.");
        }

        if (previous is not null && version is not null &&
            !version.RaisesMajorFrom(ReleaseVersion.Parse(previous, train.Grammar)!) &&
            Touching(commits, paths).Any(commit => ReleaseRules.IsBreaking(commit.Message)))
        {
            warnings.Add($"A breaking commit touches {train.Name} but {version.Major}.{version.Minor}.{version.Patch} does not raise {(version.Major == 0 ? "MINOR" : "MAJOR")} over {previous}.");
        }

        var rcTag = version is null ? null : RcTag(train, tags, version);
        if (rcTag is not null && Touching(Commits(root, $"{rcTag}..HEAD"), paths).Any(commit => ReleaseRules.IsFeature(commit.Message)))
        {
            warnings.Add($"A feat commit touches {train.Name} after {rcTag}; features are frozen once rc is tagged.");
        }

        return warnings;
    }

    public static IEnumerable<Commit> Touching(IEnumerable<Commit> commits, IReadOnlyList<string> paths) =>
        commits.Where(commit => commit.Files.Any(file => PathGlob.IsMatchAny(paths, file)));

    public static IReadOnlyList<Commit> Parse(string log) =>
        log.Split('\u001e', StringSplitOptions.RemoveEmptyEntries)
            .Select(record => record.Split('\u001f'))
            .Where(parts => parts.Length == 2)
            .Select(parts => new Commit(
                parts[0].Trim(),
                parts[1].Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .ToArray();

    private static IReadOnlyList<Commit> Commits(string root, string range) =>
        Parse(Git.Run(root, "log", "--name-only", "--format=%x1e%B%x1f", range));

    private static string? RcTag(Train train, IEnumerable<string> tags, ReleaseVersion version) =>
        tags.Select(tag => (Tag: tag, Version: ReleaseVersion.Parse(tag[train.TagPrefix.Length..], train.Grammar)))
            .Where(item => item.Version is { Label: ReleaseLabel.Rc } rc && rc.Core == version.Core)
            .OrderBy(item => item.Version)
            .Select(item => item.Tag)
            .FirstOrDefault();
}

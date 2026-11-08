using Build.Tools;
using Build.Trains;
using System.Text;

namespace Build.Releases;

public sealed record ReleasePlan(TrainVersion Version, string Notes, IReadOnlyList<string> Artifacts, IReadOnlyList<string> Refusals, IReadOnlyList<string> Warnings)
{
    public static ReleasePlan Create(string root, Train train)
    {
        var version = VersionFile.Read(root, train.VersionFile);
        var previous = TrainVersion.PickPrevious(train, Git.Tags(root, train));
        var changelog = ReadOptional(root, train.Changelog);
        var input = new ReleaseInput(
            train,
            version,
            previous,
            changelog,
            train == Train.Addin ? VersionFile.ReadProperty(root, train.VersionFile, ReleaseRules.MinUpdaterProperty) : null,
            train == Train.Addin ? ReadOptional(root, ReleaseRules.UnshippedApiFile)?.Split('\n') : null,
            train == Train.Addin ? Git.Tags(root, Train.Daemon).Select(tag => tag[Train.Daemon.TagPrefix.Length..]).ToArray() : null);

        var parsed = ReleaseVersion.Parse(version, train.Grammar);
        var trainVersion = new TrainVersion(train, version, parsed?.IsPrerelease ?? false, previous);
        var notes = ReleaseRules.HasSection(changelog, version)
            ? $"{train.Changelog} ## [{version}]"
            : $"draft from commits since {trainVersion.PreviousTag ?? "the first commit"}";

        return new ReleasePlan(
            trainVersion,
            notes,
            train.Artifacts.Select(artifact => string.Format(artifact, version)).ToArray(),
            ReleaseRules.Refusals(input),
            ContractWarnings(root, trainVersion, parsed));
    }

    public string Render()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"version: {Version.Version}");
        builder.AppendLine($"previous: {Version.Previous ?? "none"}");
        builder.AppendLine($"tag: {Version.Tag}");
        builder.AppendLine($"prerelease: {Version.IsPrerelease.ToString().ToLowerInvariant()}");
        builder.AppendLine($"latest: {Version.IsLatest.ToString().ToLowerInvariant()}");
        builder.AppendLine($"notes: {Notes}");
        builder.AppendLine($"artifacts: {string.Join(", ", Artifacts)}");
        return builder.ToString();
    }

    public void WriteGitHubFiles()
    {
        if (Environment.GetEnvironmentVariable("GITHUB_OUTPUT") is { Length: > 0 } output)
        {
            File.AppendAllText(output,
                $"version={Version.Version}\nprevious={Version.Previous}\ntag={Version.Tag}\n" +
                $"prerelease={Version.IsPrerelease.ToString().ToLowerInvariant()}\nlatest={Version.IsLatest.ToString().ToLowerInvariant()}\n");
        }

        if (Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } summary)
        {
            var builder = new StringBuilder($"### release {Version.Train.Name}\n\n| Field | Value |\n|---|---|\n");
            foreach (var line in Render().Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = line.IndexOf(": ", StringComparison.Ordinal);
                builder.Append($"| {line[..separator]} | {line[(separator + 2)..]} |\n");
            }

            File.AppendAllText(summary, builder.ToString());
        }
    }

    private static IReadOnlyList<string> ContractWarnings(string root, TrainVersion version, ReleaseVersion? parsed)
    {
        if (version.PreviousTag is null || parsed is null) return [];

        var previous = ReleaseVersion.Parse(version.Previous!, version.Train.Grammar)!;
        if (parsed.RaisesMajorFrom(previous)) return [];

        var paths = TrainPaths.Compute(root, version.Train);
        var contracts = Git.Lines(root, "diff", "--name-only", version.PreviousTag, "HEAD")
            .Where(path => PathGlob.IsMatchAny(paths, path) && File.Exists(Path.Combine(root, path)))
            .Where(path => ReleaseRules.IsContract(path, File.ReadAllText(Path.Combine(root, path))))
            .ToArray();

        return contracts.Length == 0
            ? []
            : [$"Contract files changed since {version.PreviousTag} ({string.Join(", ", contracts)}) and {version.Version} does not raise MAJOR; check the Min of each range (0055 §3)."];
    }

    private static string? ReadOptional(string root, string path)
    {
        var full = Path.Combine(root, path);
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }
}

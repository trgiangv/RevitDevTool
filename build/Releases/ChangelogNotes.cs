using Build.Tools;
using Build.Trains;
using System.Text.RegularExpressions;

namespace Build.Releases;

/// <summary>
///     Per-train changelog: the git-cliff draft of unreleased commits, and the <c>## [version]</c> section a release uses as notes.
/// </summary>
public static partial class ChangelogNotes
{
    public const string UnreleasedHeading = "## [Unreleased]";

    private const string Title = "# Changelog";

    public static string Draft(string root, Train train) =>
        Shell.Run(root, "pixi",
        [
            "run", "-e", "release", "git-cliff",
            "--config", "cliff.toml",
            "--tag-pattern", $"^{Regex.Escape(train.TagPrefix)}",
            "--unreleased", "--strip", "all",
            .. IncludePaths(train).SelectMany(path => new[] { "--include-path", path })
        ]).Trim();

    public static IReadOnlyList<string> IncludePaths(Train train) =>
        train.IsDotNet
            ? train.Roots.Select(project => $"{Path.GetDirectoryName(project)!.Replace('\\', '/')}/**").ToArray()
            : train.ExtraPaths;

    public static string ReplaceUnreleased(string? changelog, string block)
    {
        var text = (changelog ?? $"{Title}\n").Replace("\r\n", "\n");
        var heading = SectionHeading().Match(text);
        var unreleased = Regex.Match(text, @"^## \[Unreleased\].*?(?=^## \[|\z)", RegexOptions.Multiline | RegexOptions.Singleline);
        var trimmed = block.Trim();
        if (unreleased.Success)
        {
            var rest = text[(unreleased.Index + unreleased.Length)..].TrimStart('\n');
            return (text[..unreleased.Index] + trimmed + "\n\n" + rest).TrimEnd('\n') + "\n";
        }

        var at = heading.Success ? heading.Index : text.Length;
        return (text[..at].TrimEnd('\n') + "\n\n" + trimmed + "\n\n" + text[at..]).TrimEnd('\n') + "\n";
    }

    public static string? Section(string? changelog, string version)
    {
        if (changelog is null) return null;

        var match = Regex.Match(
            changelog.Replace("\r\n", "\n"),
            $@"^## \[{Regex.Escape(version)}\][^\n]*\n(?<body>.*?)(?=^## \[|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);
        return match.Success ? match.Groups["body"].Value.Trim() : null;
    }

    public static string ForRelease(string root, Train train, string version)
    {
        var path = Path.Combine(root, train.Changelog);
        var section = Section(File.Exists(path) ? File.ReadAllText(path) : null, version);
        if (section is not null) return section;

        var draft = Draft(root, train);
        return draft.StartsWith(UnreleasedHeading, StringComparison.Ordinal) ? draft[UnreleasedHeading.Length..].Trim() : draft;
    }

    public static void Update(string root, Train train)
    {
        var path = Path.Combine(root, train.Changelog);
        var current = File.Exists(path) ? File.ReadAllText(path) : null;
        var draft = Draft(root, train);
        var block = draft.Length == 0 ? $"{UnreleasedHeading}\n" : draft;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ReplaceUnreleased(current, block));
    }

    [GeneratedRegex(@"^## \[", RegexOptions.Multiline)]
    private static partial Regex SectionHeading();
}

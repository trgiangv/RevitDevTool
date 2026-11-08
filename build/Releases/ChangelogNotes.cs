using Build.Tools;
using Build.Products;
using System.Text.RegularExpressions;

namespace Build.Releases;

/// <summary>
///     Per-product changelog: the git-cliff draft of unreleased commits, and the <c>## [version]</c> section a release uses as notes.
/// </summary>
public static partial class ChangelogNotes
{
    public const string UnreleasedHeading = "## [Unreleased]";

    private const string Title = "# Changelog";

    public static string Draft(string root, Product product) =>
        Shell.Run(root, "pixi",
        [
            "run", "-e", "release", "git-cliff",
            "--config", "cliff.toml",
            "--tag-pattern", $"^{Regex.Escape(product.TagPrefix)}",
            "--unreleased", "--strip", "all",
            .. IncludePaths(product).SelectMany(path => new[] { "--include-path", path })
        ]).Trim();

    public static IReadOnlyList<string> IncludePaths(Product product) =>
        product.IsDotNet
            ? product.Roots.Select(project => $"{Path.GetDirectoryName(project)!.Replace('\\', '/')}/**").ToArray()
            : product.ExtraPaths;

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

    public static string ForRelease(string root, Product product, string version)
    {
        var path = Path.Combine(root, product.Changelog);
        var section = Section(File.Exists(path) ? File.ReadAllText(path) : null, version);
        if (section is not null) return section;

        var draft = Draft(root, product);
        return draft.StartsWith(UnreleasedHeading, StringComparison.Ordinal) ? draft[UnreleasedHeading.Length..].Trim() : draft;
    }

    public static void Update(string root, Product product)
    {
        var path = Path.Combine(root, product.Changelog);
        var current = File.Exists(path) ? File.ReadAllText(path) : null;
        var draft = Draft(root, product);
        var block = draft.Length == 0 ? $"{UnreleasedHeading}\n" : draft;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ReplaceUnreleased(current, block));
    }

    [GeneratedRegex(@"^## \[", RegexOptions.Multiline)]
    private static partial Regex SectionHeading();
}

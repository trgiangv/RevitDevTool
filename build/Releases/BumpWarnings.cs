using Build.Tools;
using Build.Products;
namespace Build.Releases;

public sealed record Commit(string Message, IReadOnlyList<string> Files);

public static class BumpWarnings
{
    public static IReadOnlyList<string> Collect(string root, Product product, string? baseSha)
    {
        if (string.IsNullOrWhiteSpace(baseSha) || baseSha.All(character => character == '0')) return [];
        if (!Git.Succeeds(root, "cat-file", "-e", $"{baseSha}^{{commit}}"))
        {
            return [$"Base {baseSha} is not in this clone; fetch the full history to get the bump warnings."];
        }

        var paths = ProductPaths.Compute(root, product);
        var changed = Git.Lines(root, "diff", "--name-only", baseSha, "HEAD");
        var commits = Commits(root, $"{baseSha}..HEAD");
        var version = ReleaseVersion.Parse(VersionFile.Read(root, product.VersionFile), product.Grammar);
        var tags = Git.Tags(root, product);
        var previous = ProductVersion.PickPrevious(product, tags);

        var warnings = new List<string>();
        if (changed.Any(path => PathGlob.IsMatchAny(paths, path)) && !changed.Contains(product.VersionFile, StringComparer.Ordinal))
        {
            warnings.Add($"Product {product.Name} changed but {product.VersionFile} did not; bump it before the next {product.Name} release.");
        }

        if (previous is not null && version is not null &&
            !version.RaisesMajorFrom(ReleaseVersion.Parse(previous, product.Grammar)!) &&
            Touching(commits, paths).Any(commit => ReleaseRules.IsBreaking(commit.Message)))
        {
            warnings.Add($"A breaking commit touches {product.Name} but {version.Major}.{version.Minor}.{version.Patch} does not raise {(version.Major == 0 ? "MINOR" : "MAJOR")} over {previous}.");
        }

        var rcTag = version is null ? null : RcTag(product, tags, version);
        if (rcTag is not null && Touching(Commits(root, $"{rcTag}..HEAD"), paths).Any(commit => ReleaseRules.IsFeature(commit.Message)))
        {
            warnings.Add($"A feat commit touches {product.Name} after {rcTag}; features are frozen once rc is tagged.");
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

    private static string? RcTag(Product product, IEnumerable<string> tags, ReleaseVersion version) =>
        tags.Select(tag => (Tag: tag, Version: ReleaseVersion.Parse(tag[product.TagPrefix.Length..], product.Grammar)))
            .Where(item => item.Version is { Label: ReleaseLabel.Rc } rc && rc.Core == version.Core)
            .OrderBy(item => item.Version)
            .Select(item => item.Tag)
            .FirstOrDefault();
}

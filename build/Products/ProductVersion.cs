namespace Build.Products;

public sealed record ProductVersion(Product Product, string Version, bool IsPrerelease, string? Previous)
{
    public string Tag => Product.Tag(Version);

    public string? PreviousTag => Previous is null ? null : Product.Tag(Previous);

    public bool IsLatest => Product == Product.Daemon && !IsPrerelease;

    /// <summary>
    ///     The release version in the product's version file; a <c>0.0.0</c> or malformed version is refused.
    /// </summary>
    public static ProductVersion FromFile(string root, Product product)
    {
        var text = VersionFile.Read(root, product.VersionFile);
        return !text.StartsWith("0.0.0", StringComparison.Ordinal) && ReleaseVersion.Parse(text, product.Grammar) is { } parsed
            ? new ProductVersion(product, text, parsed.IsPrerelease, null)
            : throw new InvalidOperationException($"Version '{text}' is not a release version; set it in {product.VersionFile}.");
    }

    public static string NonRelease(Product product, string? runNumber, DateTimeOffset now)
    {
        var stamp = now.UtcDateTime.ToString("yyyyMMddHHmmss");
        var hasRun = !string.IsNullOrWhiteSpace(runNumber);
        return product.Grammar == VersionGrammar.Pep440
            ? $"0.0.0.dev{(hasRun ? runNumber : stamp)}"
            : hasRun ? $"0.0.0-ci.{runNumber}" : $"0.0.0-local.{stamp}";
    }

    public static string? PickPrevious(Product product, IEnumerable<string> tags) =>
        tags.Where(tag => tag.StartsWith(product.TagPrefix, StringComparison.Ordinal))
            .Select(tag => tag[product.TagPrefix.Length..])
            .Select(version => (Text: version, Parsed: ReleaseVersion.Parse(version, product.Grammar)))
            .Where(item => item.Parsed is not null)
            .OrderByDescending(item => item.Parsed)
            .Select(item => item.Text)
            .FirstOrDefault();

    /// <summary>
    ///     The newest release below <paramref name="version"/> with the same prerelease state, so it shares the Velopack channel.
    /// </summary>
    public static string? PickSameChannel(Product product, IEnumerable<string> tags, string version)
    {
        var current = ReleaseVersion.Parse(version, product.Grammar);
        return current is null
            ? null
            : PickPrevious(product, tags.Where(tag =>
                tag.StartsWith(product.TagPrefix, StringComparison.Ordinal) &&
                ReleaseVersion.Parse(tag[product.TagPrefix.Length..], product.Grammar) is { } parsed &&
                parsed.IsPrerelease == current.IsPrerelease &&
                parsed.CompareTo(current) < 0));
    }
}

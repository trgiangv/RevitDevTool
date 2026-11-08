using Build.Tools;
using Build.Products;
using DevTools.Update;
using DevTools.Update.Releases;

namespace Build.Releases;

/// <summary>
///     <c>build -- channels &lt;product&gt; --version &lt;v&gt;</c>: moves the product's entries in <c>channels.json</c>, signs it,
///     and replaces it in the rolling <c>channels</c> GitHub Release (0059 §2).
/// </summary>
public static class Channels
{
    public const string ReleaseTag = "channels";
    private const string SetupName = "RevitDevTool-Setup.exe";

    private static readonly Product[] IndexProducts = [Product.Daemon, Product.Addin];

    public static Uri IndexUrl { get; } = new UpdateEngineOptions().ChannelIndexUrl;

    public static Uri Downloads { get; } = new(IndexUrl, "../");

    /// <summary>
    ///     A stable version sets the stable entry. The prerelease entry of each product is the newest of its prerelease and its stable.
    /// </summary>
    public static ChannelIndex Advance(ChannelIndex index, Product product, string version, Uri downloads)
    {
        var parsed = Check(product, version);
        var stable = index.Stable;
        var prerelease = index.Prerelease;
        foreach (var each in IndexProducts)
        {
            var stableVersion = VersionOf(stable, each);
            var prereleaseVersion = VersionOf(prerelease, each);
            if (each == product)
            {
                if (!parsed.IsPrerelease) stableVersion = version;
                prereleaseVersion = Newest(prereleaseVersion, version);
            }

            prereleaseVersion = Newest(prereleaseVersion, stableVersion);
            stable = With(stable, each, stableVersion, downloads);
            prerelease = With(prerelease, each, prereleaseVersion, downloads);
        }

        return index with { Stable = stable, Prerelease = prerelease };
    }

    public static IEnumerable<Uri> Urls(ChannelIndex index) =>
        new[] { index.Stable, index.Prerelease }
            .SelectMany(channel => new[] { channel.Daemon?.Feed is { } feed ? new Uri(feed, SetupName) : null, channel.Addin?.Manifest })
            .OfType<Uri>()
            .Distinct();

    public static async Task<string> RunAsync(string root, Product product, string version)
    {
        Check(product, version);
        var signingKey = Signing.Key();

        var output = Directory.CreateDirectory(Path.Combine(root, "output", ReleaseTag)).FullName;
        var jsonPath = Path.Combine(output, ChannelIndex.FileName);
        var sigPath = jsonPath + ReleaseSignature.Extension;
        var exists = Gh.ReleaseExists(root, ReleaseTag);
        if (exists) Gh.Download(root, ReleaseTag, output, ChannelIndex.FileName);

        var current = exists && File.Exists(jsonPath) ? ChannelIndex.FromJson(await File.ReadAllBytesAsync(jsonPath)) : new ChannelIndex();
        var next = Advance(current, product, version, Downloads);
        await CheckUrlsAsync(next);

        var json = next.ToJson();
        await File.WriteAllBytesAsync(jsonPath, json);
        await File.WriteAllTextAsync(sigPath, Signing.Sign(json, signingKey));

        if (!exists)
            Gh.Run(root, "release", "create", ReleaseTag, "--title", "Update channels", "--notes", $"The signed {ChannelIndex.FileName} the Daemon reads.", "--latest=false");
        Gh.Upload(root, ReleaseTag, jsonPath);
        Gh.Upload(root, ReleaseTag, sigPath);

        return $"{ChannelIndex.FileName} names {product.Name} {version}.";
    }

    private static async Task CheckUrlsAsync(ChannelIndex index)
    {
        using var http = new HttpClient();
        foreach (var url in Urls(index))
        {
            using var response = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"{url} answered {(int)response.StatusCode}; publish that release before {ChannelIndex.FileName} names it.");
        }
    }

    private static ReleaseVersion Check(Product product, string version)
    {
        if (!IndexProducts.Contains(product)) throw new InvalidOperationException($"Product {product.Name} has no entry in {ChannelIndex.FileName}; use daemon or addin.");

        return Parse(version) is { } parsed && parsed.Core != (0, 0, 0)
            ? parsed
            : throw new InvalidOperationException($"Version '{version}' is not a release version ({ReleaseVersion.Describe(VersionGrammar.SemVer)}).");
    }

    private static ReleaseVersion? Parse(string? version) => version is null ? null : ReleaseVersion.Parse(version, VersionGrammar.SemVer);

    private static string? Newest(string? left, string? right) =>
        left is null ? right
        : right is null ? left
        : Parse(right)!.CompareTo(Parse(left)) > 0 ? right : left;

    private static string? VersionOf(Channel channel, Product product) => product == Product.Daemon ? channel.Daemon?.Version : channel.Addin?.Version;

    private static Channel With(Channel channel, Product product, string? version, Uri downloads) => version switch
    {
        null => channel,
        _ when product == Product.Daemon => channel with { Daemon = new DaemonEntry(version, new Uri(downloads, product.Tag(version) + "/")) },
        _ => channel with { Addin = new AddinEntry(version, new Uri(downloads, $"{product.Tag(version)}/{UpdateManifest.FileName}")) }
    };
}

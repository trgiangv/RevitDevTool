using Build.Products;
using DevTools.Update;
using DevTools.Update.Engine;
using DevTools.Update.Releases;
using Microsoft.Extensions.Configuration;
using Sourcy.DotNet;
using System.Security.Cryptography;
using System.Text;
using Velopack;

namespace Build.Releases;

/// <summary>
///     The GitHub Release assets of a <c>daemon</c> or <c>addin</c> release, and the check that reads them the way a client does (0054).
/// </summary>
public static class ReleaseAssets
{
    public const string SetupName = "RevitDevTool-Setup.exe";
    public const string BundleName = "RevitDevTool.bundle.zip";

    public static string VelopackChannel(bool prerelease) => prerelease ? DaemonUpdater.PrereleaseChannel : DaemonUpdater.StableChannel;

    public static string FeedName(ProductVersion version) => version.Product == Product.Addin
        ? UpdateManifest.FileName
        : $"releases.{VelopackChannel(version.IsPrerelease)}.json";

    /// <summary>
    ///     Payloads first, then the feeds, then their signatures, so a client never reads a feed that names a missing asset.
    /// </summary>
    public static (IReadOnlyList<string> Payloads, IReadOnlyList<string> Feeds) Split(ProductVersion version, IEnumerable<string> names)
    {
        var all = names.ToArray();
        var payloads = all.Where(name => IsPayload(version, name)).Order(StringComparer.Ordinal).ToArray();
        var feeds = all.Where(name => IsFeed(version, name))
            .OrderBy(name => name.EndsWith(ReleaseSignature.Extension, StringComparison.Ordinal))
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToArray();
        return (payloads, feeds);
    }

    /// <summary>
    ///     Reads the signed feed, checks its signature when <paramref name="signed"/>, and checks every file it names for this version.
    /// </summary>
    public static void Verify(ProductVersion version, string output, bool signed)
    {
        var signature = new ReleaseSignature(ShippedPublicKey());
        if (signed && !signature.IsRequired)
            throw new InvalidOperationException("Update:PublicKey in the Daemon appsettings.json is empty; clients could not verify this release.");

        var feedPath = Path.Combine(output, FeedName(version));
        Check(File.Exists(feedPath), $"{FeedName(version)} is missing from {output}.");
        var data = File.ReadAllBytes(feedPath);
        if (signed)
        {
            var sigPath = feedPath + ReleaseSignature.Extension;
            Check(File.Exists(sigPath), $"{Path.GetFileName(sigPath)} is missing.");
            signature.Verify(Path.GetFileName(feedPath), data, File.ReadAllBytes(sigPath));
        }

        if (version.Product == Product.Addin)
        {
            var manifest = UpdateManifest.FromJson(data);
            Check(manifest.Version == version.Version, $"The manifest version {manifest.Version} is not {version.Version}.");
            foreach (var payload in manifest.Payloads) VerifyFile(Path.Combine(output, payload.Asset), payload.Sha256, payload.Size);
            Check(File.Exists(Path.Combine(output, BundleName)), $"{BundleName} is missing.");
            return;
        }

        var feed = VelopackAssetFeed.FromJson(Encoding.UTF8.GetString(data));
        if (DeltaBaseProblem(feed, version.Version) is { } problem) throw new InvalidOperationException(problem);
        foreach (var asset in feed.Assets.Where(asset => asset.Version.ToString() == version.Version))
            VerifyFile(Path.Combine(output, asset.FileName), asset.SHA256, asset.Size);
        Check(File.Exists(Path.Combine(output, SetupName)), $"{SetupName} is missing.");
    }

    /// <summary>
    ///     The feed must list the full package of this version, deltas of this version only, and at most one older full package: the delta base.
    ///     A client on an older version then finds no base and takes the full package (0059 §7).
    /// </summary>
    public static string? DeltaBaseProblem(VelopackAssetFeed feed, string version)
    {
        var assets = feed.Assets;
        if (!assets.Any(asset => asset.Type == VelopackAssetType.Full && asset.Version.ToString() == version))
            return $"The Velopack feed has no full package of {version}.";

        var older = assets.Where(asset => asset.Version.ToString() != version).ToArray();
        if (older.Any(asset => asset.Type != VelopackAssetType.Full) || older.Length > 1)
            return $"The Velopack feed of {version} lists older packages besides one delta base: {string.Join(", ", older.Select(asset => asset.FileName))}.";

        return null;
    }

    private static bool IsPayload(ProductVersion version, string name)
    {
        var prefix = $"RevitDevTool-{version.Version}-";
        return version.Product == Product.Addin
            ? name == BundleName || (name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".zip", StringComparison.Ordinal))
            : name == SetupName || (name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".nupkg", StringComparison.Ordinal));
    }

    private static bool IsFeed(ProductVersion version, string name)
    {
        var json = name.EndsWith(ReleaseSignature.Extension, StringComparison.Ordinal) ? name[..^ReleaseSignature.Extension.Length] : name;
        return version.Product == Product.Addin
            ? json == UpdateManifest.FileName
            : (json.StartsWith("releases.", StringComparison.Ordinal) || json.StartsWith("assets.", StringComparison.Ordinal)) && json.EndsWith(".json", StringComparison.Ordinal);
    }

    private static string? ShippedPublicKey() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(Path.GetDirectoryName(Projects.DevTools_Daemon.FullName)!, "appsettings.json"))
        .Build()
        .GetSection(UpdateEngineOptions.SectionName)
        .Get<UpdateEngineOptions>()?
        .PublicKey;

    private static void VerifyFile(string path, string sha256, long size)
    {
        Check(File.Exists(path), $"{Path.GetFileName(path)} is missing.");

        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        Check(string.Equals(actual, sha256, StringComparison.OrdinalIgnoreCase) && stream.Length == size,
            $"{Path.GetFileName(path)} does not match its feed entry.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

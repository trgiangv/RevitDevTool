using System.Text;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace DevTools.Update.Releases;

/// <summary>
///     Velopack <see cref="IUpdateSource"/> for one <c>daemon</c> <see cref="Release"/>. Velopack stays the installer engine.
///     It reads <c>releases.&lt;channel&gt;.json</c> only if its signature is correct. All engine versions must keep this contract (0052).
/// </summary>
/// <remarks>
///     A release holds only the delta from the version before it, and Velopack applies a delta to any older base without an error.
///     So the feed keeps its deltas only when it lists the full package of the installed version; otherwise Velopack takes the full package (0059 §7).
/// </remarks>
public sealed class VelopackReleaseSource(ReleaseReader reader, Release release) : IUpdateSource
{
    public async Task<VelopackAssetFeed> GetReleaseFeed(IVelopackLogger logger, string? appId, string channel, Guid? stagingId = null, VelopackAsset? latestLocalRelease = null)
    {
        var data = await reader.ReadSignedAsync(release, $"releases.{channel}.json", CancellationToken.None).ConfigureAwait(false);
        return WithUsableDeltas(VelopackAssetFeed.FromJson(Encoding.UTF8.GetString(data)), latestLocalRelease);
    }

    public Task DownloadReleaseEntry(IVelopackLogger logger, VelopackAsset releaseEntry, string localFile, Action<int> progress, CancellationToken cancelToken = default) =>
        reader.DownloadAsync(release.Asset(releaseEntry.FileName), localFile, progress, cancelToken);

    private static VelopackAssetFeed WithUsableDeltas(VelopackAssetFeed feed, VelopackAsset? installed)
    {
        var hasBase = installed is not null && feed.Assets.Any(asset => asset.Type == VelopackAssetType.Full && asset.Version == installed.Version);
        return hasBase ? feed : new VelopackAssetFeed { Assets = feed.Assets.Where(asset => asset.Type != VelopackAssetType.Delta).ToArray() };
    }
}

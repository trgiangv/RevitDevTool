using System.Text;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace DevTools.Update.Releases;

/// <summary>
///     Velopack <see cref="IUpdateSource"/> for one <see cref="Release"/> of any provider. Velopack stays the installer engine.
///     It reads <c>releases.&lt;channel&gt;.json</c> only if its signature is correct. All engine versions must keep this contract (0052).
/// </summary>
public sealed class VelopackReleaseSource(ReleaseReader reader, Release release) : IUpdateSource
{
    public async Task<VelopackAssetFeed> GetReleaseFeed(IVelopackLogger logger, string? appId, string channel, Guid? stagingId = null, VelopackAsset? latestLocalRelease = null)
    {
        var data = await reader.ReadSignedAsync(release, $"releases.{channel}.json", CancellationToken.None).ConfigureAwait(false);
        return VelopackAssetFeed.FromJson(Encoding.UTF8.GetString(data));
    }

    public Task DownloadReleaseEntry(IVelopackLogger logger, VelopackAsset releaseEntry, string localFile, Action<int> progress, CancellationToken cancelToken = default) =>
        reader.DownloadAsync(release.Asset(releaseEntry.FileName), localFile, progress, cancelToken);
}

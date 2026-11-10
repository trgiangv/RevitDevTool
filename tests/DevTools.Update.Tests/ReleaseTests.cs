using DevTools.Update.Engine;
using DevTools.Update.Releases;
using System.Security.Cryptography;
using System.Text;
using static DevTools.Update.Tests.Hosts;

namespace DevTools.Update.Tests;

[TestClass]
public sealed class ReleaseTests
{
    [TestMethod]
    public void Signature_in_the_build_pipeline_format_verifies_and_a_changed_byte_fails()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var data = Encoding.UTF8.GetBytes("{\"schema\":1}");
        var signature = Encoding.ASCII.GetBytes(Convert.ToBase64String(key.SignData(data, HashAlgorithmName.SHA256)));
        var verifier = new ReleaseSignature(key.ExportSubjectPublicKeyInfoPem());

        verifier.Verify("feed", data, signature);

        data[0] ^= 1;
        Assert.ThrowsExactly<UpdateIntegrityException>(() => verifier.Verify("feed", data, signature));
    }

    [TestMethod]
    public void A_key_that_is_not_P256_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        Assert.ThrowsExactly<ArgumentException>(() => new ReleaseSignature(key.ExportSubjectPublicKeyInfoPem()));
    }

    [TestMethod]
    public async Task Manifest_with_a_signature_from_another_key_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var releases = new FakeChannels(other);
        var release = releases.PublishAddin("3.1.0", (Revit("2024"), "new"));
        var reader = new ReleaseReader(new HttpClient(releases), new ReleaseSignature(key.ExportSubjectPublicKeyInfoPem()));

        await Assert.ThrowsExactlyAsync<UpdateIntegrityException>(() => reader.ReadManifestAsync(release, CancellationToken.None));
    }

    [TestMethod]
    public async Task Manifest_without_a_signature_is_refused_when_a_key_is_set()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var releases = new FakeChannels();
        var release = releases.PublishAddin("3.1.0", (Revit("2024"), "new"));
        var reader = new ReleaseReader(new HttpClient(releases), new ReleaseSignature(key.ExportSubjectPublicKeyInfoPem()));

        await Assert.ThrowsExactlyAsync<UpdateIntegrityException>(() => reader.ReadManifestAsync(release, CancellationToken.None));
    }

    [TestMethod]
    public async Task Manifest_has_one_payload_per_host()
    {
        var releases = new FakeChannels();
        var release = releases.PublishAddin("3.1.0", (Revit("2024"), "new"), (Acad("2024"), "new"));
        var reader = new ReleaseReader(new HttpClient(releases), new ReleaseSignature(null));

        var manifest = await reader.ReadManifestAsync(release, CancellationToken.None);

        Assert.AreEqual("AcadDevTool-3.1.0-2024.zip", manifest.FindPayload(Acad("2024"))!.Asset);
        Assert.AreEqual("RevitDevTool-3.1.0-2024.zip", manifest.FindPayload(Revit("2024"))!.Asset);
        Assert.IsNull(manifest.FindPayload(Revit("2025")));
    }

    [TestMethod]
    public async Task Missing_manifest_is_integrity_error()
    {
        var reader = new ReleaseReader(new HttpClient(new FakeChannels()), new ReleaseSignature(null));
        var release = new Release("3.1.0", false, new Uri("https://releases.example.test/addin/v3.1.0/"));

        await Assert.ThrowsExactlyAsync<UpdateIntegrityException>(() => reader.ReadManifestAsync(release, CancellationToken.None));
    }

    [TestMethod]
    public void Schema_two_index_is_refused()
    {
        Assert.ThrowsExactly<UpdateIntegrityException>(() => ChannelIndex.FromJson("{\"schema\":2}"u8.ToArray()));
    }

    [TestMethod]
    public void Index_round_trips_camel_case()
    {
        var index = new ChannelIndex
        {
            Stable = new Channel { Addin = new AddinEntry("4.0.0", new Uri("https://example.test/addin/v4.0.0/update-manifest.json")) },
            Prerelease = new Channel { Daemon = new DaemonEntry("4.1.0-beta.1", new Uri("https://example.test/daemon/v4.1.0-beta.1/")) }
        };

        var json = Encoding.UTF8.GetString(index.ToJson());
        var (daemon, addin) = ChannelIndex.FromJson(index.ToJson()).Pick(prerelease: true);

        StringAssert.Contains(json, "\"manifest\": \"https://example.test/addin/v4.0.0/update-manifest.json\"");
        Assert.IsNull(addin);
        Assert.AreEqual(new Release("4.1.0-beta.1", true, new Uri("https://example.test/daemon/v4.1.0-beta.1/")), daemon);
        Assert.AreEqual(new Uri("https://example.test/addin/v4.0.0/"), ChannelIndex.FromJson(index.ToJson()).Pick(prerelease: false).Addin!.BaseUrl);
    }

    [TestMethod]
    public async Task One_behind_keeps_the_delta()
    {
        var feed = await ReadFeedAsync("0.0.2");

        CollectionAssert.AreEquivalent(new[] { "RevitDevTool-0.0.2-full.nupkg", "RevitDevTool-0.0.3-full.nupkg", "RevitDevTool-0.0.3-delta.nupkg" },
            feed.Assets.Select(asset => asset.FileName).ToArray());
    }

    [TestMethod]
    public async Task Two_behind_gets_only_full()
    {
        var feed = await ReadFeedAsync("0.0.1");

        CollectionAssert.AreEquivalent(new[] { "RevitDevTool-0.0.2-full.nupkg", "RevitDevTool-0.0.3-full.nupkg" },
            feed.Assets.Select(asset => asset.FileName).ToArray());
    }

    private static async Task<Velopack.VelopackAssetFeed> ReadFeedAsync(string installed)
    {
        const string json = """
            {"Assets":[
              {"PackageId":"RevitDevTool","Version":"0.0.2","Type":"Full","FileName":"RevitDevTool-0.0.2-full.nupkg","SHA1":"A","SHA256":"A","Size":1},
              {"PackageId":"RevitDevTool","Version":"0.0.3","Type":"Full","FileName":"RevitDevTool-0.0.3-full.nupkg","SHA1":"B","SHA256":"B","Size":1},
              {"PackageId":"RevitDevTool","Version":"0.0.3","Type":"Delta","FileName":"RevitDevTool-0.0.3-delta.nupkg","SHA1":"C","SHA256":"C","Size":1}
            ]}
            """;
        var channels = new FakeChannels();
        var release = channels.PublishDaemon("0.0.3");
        channels.Add(release.Asset("releases.win.json"), Encoding.UTF8.GetBytes(json));
        var source = new VelopackReleaseSource(new ReleaseReader(new HttpClient(channels), new ReleaseSignature(null)), release);
        var local = new Velopack.VelopackAsset { PackageId = "RevitDevTool", Version = Velopack.SemanticVersion.Parse(installed), Type = Velopack.VelopackAssetType.Full };

        return await source.GetReleaseFeed(Velopack.Logging.NullVelopackLogger.Instance, "RevitDevTool", "win", latestLocalRelease: local);
    }

    [TestMethod]
    public async Task Download_starts_again_when_the_connection_drops()
    {
        var releases = new FakeChannels { DroppedResponses = ReleaseReader.Attempts - 1 };
        var release = releases.PublishAddin("3.1.0", (Revit("2024"), "new"));
        var reader = new ReleaseReader(new HttpClient(releases), new ReleaseSignature(null)) { RetryDelay = TimeSpan.Zero };
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip");

        try
        {
            var sha256 = await reader.DownloadAsync(release.Asset("RevitDevTool-3.1.0-2024.zip"), path, null, CancellationToken.None);

            Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(FakeChannels.PayloadZip("new"))), sha256);
            Assert.AreEqual(ReleaseReader.Attempts, releases.FileRequests);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Download_stops_after_the_last_attempt()
    {
        var releases = new FakeChannels { DroppedResponses = ReleaseReader.Attempts };
        var release = releases.PublishAddin("3.1.0", (Revit("2024"), "new"));
        var reader = new ReleaseReader(new HttpClient(releases), new ReleaseSignature(null)) { RetryDelay = TimeSpan.Zero };

        await Assert.ThrowsAsync<HttpRequestException>(() => reader.ReadManifestAsync(release, CancellationToken.None));
        Assert.AreEqual(ReleaseReader.Attempts, releases.FileRequests);
    }

    [TestMethod]
    public async Task Missing_asset_is_not_downloaded_again()
    {
        var releases = new FakeChannels();
        var reader = new ReleaseReader(new HttpClient(releases), new ReleaseSignature(null)) { RetryDelay = TimeSpan.Zero };

        await Assert.ThrowsAsync<HttpRequestException>(() => reader.DownloadAsync(new Uri("https://releases.example.test/missing.zip"), Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip"), null, CancellationToken.None));
        Assert.AreEqual(1, releases.FileRequests);
    }

    [TestMethod]
    public async Task Cached_index_asks_once()
    {
        var channels = new FakeChannels();
        var time = new ManualTime();
        var source = new CachedChannelIndexSource(
            new ChannelIndexSource(new ReleaseReader(new HttpClient(channels), new ReleaseSignature(null)), FakeChannels.IndexUrl), time);

        await source.GetAsync(CancellationToken.None);
        await source.GetAsync(CancellationToken.None);
        Assert.AreEqual(1, channels.IndexRequests);

        time.Now += CachedChannelIndexSource.Lifetime;
        await source.GetAsync(CancellationToken.None);
        Assert.AreEqual(2, channels.IndexRequests);
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [TestMethod]
    public void Backoff_doubles_and_stops_at_one_day()
    {
        var hour = TimeSpan.FromHours(1);
        Assert.AreEqual(TimeSpan.FromHours(2), UpdateScheduler.Backoff(hour, 1));
        Assert.AreEqual(TimeSpan.FromHours(24), UpdateScheduler.Backoff(hour, 9));
        Assert.AreEqual(TimeSpan.FromMinutes(48), UpdateScheduler.Jitter(hour, 0));
        Assert.AreEqual(TimeSpan.FromMinutes(72), UpdateScheduler.Jitter(hour, 1));
    }
}

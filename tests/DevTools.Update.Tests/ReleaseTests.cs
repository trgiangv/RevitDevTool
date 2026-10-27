using DevTools.Update.Engine;
using DevTools.Update.Releases;
using System.Security.Cryptography;
using System.Text;

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
        var releases = new FakeReleases();
        var release = releases.Publish("3.1.0", other, ("2024", ["Revit"], "new"));
        var reader = new ReleaseReader(new HttpClient(releases), new ReleaseSignature(key.ExportSubjectPublicKeyInfoPem()));

        await Assert.ThrowsExactlyAsync<UpdateIntegrityException>(() => reader.ReadManifestAsync(release, CancellationToken.None));
    }

    [TestMethod]
    public async Task Manifest_without_a_signature_is_refused_when_a_key_is_set()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var releases = new FakeReleases();
        var release = releases.Publish("3.1.0", null, ("2024", ["Revit"], "new"));
        var reader = new ReleaseReader(new HttpClient(releases), new ReleaseSignature(key.ExportSubjectPublicKeyInfoPem()));

        await Assert.ThrowsExactlyAsync<UpdateIntegrityException>(() => reader.ReadManifestAsync(release, CancellationToken.None));
    }

    [TestMethod]
    public async Task Manifest_parses_payloads_with_string_host_versions()
    {
        var releases = new FakeReleases();
        var release = releases.Publish("3.1.0", null, ("2024", ["AutoCAD*", "Revit"], "new"));
        var reader = new ReleaseReader(new HttpClient(releases), new ReleaseSignature(null));

        var manifest = await reader.ReadManifestAsync(release, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "AutoCAD*", "Revit" }, manifest.FindPayload("2024")!.Platforms.ToArray());
        Assert.IsNull(manifest.FindPayload("2025"));
    }

    [TestMethod]
    public void Release_without_the_asset_fails_with_an_integrity_error()
    {
        var release = new Release("3.1.0", false, new Dictionary<string, Uri>());
        Assert.ThrowsExactly<UpdateIntegrityException>(() => release.Asset("update-manifest.json"));
    }

    [TestMethod]
    public async Task Download_starts_again_when_the_connection_drops()
    {
        var releases = new FakeReleases { DroppedResponses = ReleaseReader.Attempts - 1 };
        var release = releases.Publish("3.1.0", null, ("2024", ["Revit"], "new"));
        var reader = new ReleaseReader(new HttpClient(releases), new ReleaseSignature(null)) { RetryDelay = TimeSpan.Zero };
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip");

        try
        {
            var sha256 = await reader.DownloadAsync(release.Asset("RevitDevTool-3.1.0-2024.zip"), path, null, CancellationToken.None);

            Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(FakeReleases.PayloadZip("new"))), sha256);
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
        var releases = new FakeReleases { DroppedResponses = ReleaseReader.Attempts };
        var release = releases.Publish("3.1.0", null, ("2024", ["Revit"], "new"));
        var reader = new ReleaseReader(new HttpClient(releases), new ReleaseSignature(null)) { RetryDelay = TimeSpan.Zero };

        await Assert.ThrowsAsync<HttpRequestException>(() => reader.ReadManifestAsync(release, CancellationToken.None));
        Assert.AreEqual(ReleaseReader.Attempts, releases.FileRequests);
    }

    [TestMethod]
    public async Task Missing_asset_is_not_downloaded_again()
    {
        var releases = new FakeReleases();
        var reader = new ReleaseReader(new HttpClient(releases), new ReleaseSignature(null)) { RetryDelay = TimeSpan.Zero };

        await Assert.ThrowsAsync<HttpRequestException>(() => reader.DownloadAsync(new Uri("https://releases.example.test/missing.zip"), Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip"), null, CancellationToken.None));
        Assert.AreEqual(1, releases.FileRequests);
    }

    [TestMethod]
    public async Task Cached_provider_asks_one_time_in_the_lifetime()
    {
        var releases = new FakeReleases();
        releases.Publish("3.1.0", null, ("2024", ["Revit"], "new"));
        var time = new ManualTime();
        var provider = new CachedReleaseProvider(releases, time);

        await provider.GetLatestAsync(false, CancellationToken.None);
        await provider.GetLatestAsync(false, CancellationToken.None);
        Assert.AreEqual(1, releases.Requests);

        time.Now += CachedReleaseProvider.Lifetime;
        await provider.GetLatestAsync(false, CancellationToken.None);
        Assert.AreEqual(2, releases.Requests);
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [TestMethod]
    public void Backoff_doubles_obeys_retry_after_and_stops_at_one_day()
    {
        var hour = TimeSpan.FromHours(1);
        Assert.AreEqual(TimeSpan.FromHours(2), UpdateScheduler.Backoff(hour, 1));
        Assert.AreEqual(TimeSpan.FromHours(5), UpdateScheduler.Backoff(hour, 1, TimeSpan.FromHours(5)));
        Assert.AreEqual(TimeSpan.FromHours(24), UpdateScheduler.Backoff(hour, 9));
        Assert.AreEqual(TimeSpan.FromMinutes(48), UpdateScheduler.Jitter(hour, 0));
        Assert.AreEqual(TimeSpan.FromMinutes(72), UpdateScheduler.Jitter(hour, 1));
    }
}

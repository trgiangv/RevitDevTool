using Velopack;

namespace Build.Tests;

[TestClass]
public sealed class ReleaseAssetsTests
{
    private static readonly TrainVersion DaemonBeta = new(Train.Daemon, "4.0.0-beta.9", true, null);
    private static readonly TrainVersion AddinStable = new(Train.Addin, "4.0.0", false, null);

    [TestMethod]
    public void Daemon_feeds_go_last()
    {
        var (payloads, feeds) = ReleaseAssets.Split(DaemonBeta,
        [
            "releases.prerelease.json.sig", "notes.md", "RevitDevTool-4.0.0-beta.9-prerelease-full.nupkg", "assets.prerelease.json",
            "releases.prerelease.json", "RevitDevTool-Setup.exe", "RevitDevTool-4.0.0-beta.9-prerelease-delta.nupkg",
            "RevitDevTool-4.0.0-beta.8-prerelease-full.nupkg"
        ]);

        CollectionAssert.AreEqual(
            new[] { "RevitDevTool-4.0.0-beta.9-prerelease-delta.nupkg", "RevitDevTool-4.0.0-beta.9-prerelease-full.nupkg", "RevitDevTool-Setup.exe" },
            payloads.ToArray());
        CollectionAssert.AreEqual(
            new[] { "assets.prerelease.json", "releases.prerelease.json", "releases.prerelease.json.sig" },
            feeds.ToArray());
    }

    [TestMethod]
    public void Addin_assets_per_year()
    {
        var (payloads, feeds) = ReleaseAssets.Split(AddinStable,
        [
            "update-manifest.json.sig", "RevitDevTool-4.0.0-2025.zip", "update-manifest.json", "RevitDevTool.bundle.zip",
            "RevitDevTool-4.0.0-2026.zip", "RevitDevTool-3.9.0-2025.zip", "notes.md"
        ]);

        CollectionAssert.AreEqual(new[] { "RevitDevTool-4.0.0-2025.zip", "RevitDevTool-4.0.0-2026.zip", "RevitDevTool.bundle.zip" }, payloads.ToArray());
        CollectionAssert.AreEqual(new[] { "update-manifest.json", "update-manifest.json.sig" }, feeds.ToArray());
    }

    [TestMethod]
    [DataRow(true, "releases.prerelease.json")]
    [DataRow(false, "releases.win.json")]
    public void Daemon_feed_follows_channel(bool prerelease, string feed)
    {
        Assert.AreEqual(feed, ReleaseAssets.FeedName(new TrainVersion(Train.Daemon, "4.0.0", prerelease, null)));
    }

    [TestMethod]
    public void Feed_with_one_base_passes()
    {
        Assert.IsNull(ReleaseAssets.DeltaBaseProblem(Feed(("4.0.0-beta.9", "Full"), ("4.0.0-beta.9", "Delta"), ("4.0.0-beta.8", "Full")), "4.0.0-beta.9"));
    }

    [TestMethod]
    public void Refuses_older_delta_in_feed()
    {
        Assert.IsNotNull(ReleaseAssets.DeltaBaseProblem(
            Feed(("4.0.0-beta.9", "Full"), ("4.0.0-beta.9", "Delta"), ("4.0.0-beta.8", "Full"), ("4.0.0-beta.8", "Delta")), "4.0.0-beta.9"));
    }

    [TestMethod]
    public void Refuses_feed_without_full()
    {
        Assert.IsNotNull(ReleaseAssets.DeltaBaseProblem(Feed(("4.0.0-beta.9", "Delta"), ("4.0.0-beta.8", "Full")), "4.0.0-beta.9"));
    }

    [TestMethod]
    [DataRow("daemon", "RevitDevTool Daemon 4.0.0")]
    [DataRow("addin", "RevitDevTool add-in 4.0.0")]
    public void Release_title_names_train(string train, string title)
    {
        Assert.AreEqual(title, Publish.Title(new TrainVersion(Train.Find(train)!, "4.0.0", false, null)));
    }

    private static VelopackAssetFeed Feed(params (string Version, string Type)[] assets) =>
        VelopackAssetFeed.FromJson("{\"Assets\":[" + string.Join(",", assets.Select(asset =>
            $"{{\"PackageId\":\"RevitDevTool\",\"Version\":\"{asset.Version}\",\"Type\":\"{asset.Type}\",\"FileName\":\"RevitDevTool-{asset.Version}-{asset.Type.ToLowerInvariant()}.nupkg\",\"SHA256\":\"00\",\"Size\":1}}")) + "]}");
}

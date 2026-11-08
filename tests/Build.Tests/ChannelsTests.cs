using DevTools.Update.Releases;

namespace Build.Tests;

[TestClass]
public sealed class ChannelsTests
{
    private static readonly Uri Downloads = new("https://github.com/o/r/releases/download/");

    [TestMethod]
    public void First_stable_fills_both_sides()
    {
        var index = Channels.Advance(new ChannelIndex(), Train.Daemon, "4.0.0", Downloads);

        Assert.AreEqual("4.0.0", index.Stable.Daemon!.Version);
        Assert.AreEqual("4.0.0", index.Prerelease.Daemon!.Version);
        Assert.AreEqual(new Uri("https://github.com/o/r/releases/download/daemon/v4.0.0/"), index.Stable.Daemon.Feed);
        Assert.IsNull(index.Stable.Addin);
    }

    [TestMethod]
    public void Prerelease_moves_prerelease_only()
    {
        var index = Advance(("addin", "4.0.0"), ("addin", "4.1.0-beta.1"));

        Assert.AreEqual("4.0.0", index.Stable.Addin!.Version);
        Assert.AreEqual("4.1.0-beta.1", index.Prerelease.Addin!.Version);
        Assert.AreEqual(new Uri("https://github.com/o/r/releases/download/addin/v4.1.0-beta.1/update-manifest.json"), index.Prerelease.Addin.Manifest);
    }

    [TestMethod]
    public void Stable_overtakes_open_prerelease()
    {
        var index = Advance(("addin", "4.0.0-rc.1"), ("addin", "4.0.0"));

        Assert.AreEqual("4.0.0", index.Stable.Addin!.Version);
        Assert.AreEqual("4.0.0", index.Prerelease.Addin!.Version);
    }

    [TestMethod]
    public void Hotfix_keeps_newer_prerelease()
    {
        var index = Advance(("daemon", "4.0.0"), ("daemon", "4.1.0-beta.1"), ("daemon", "4.0.1"));

        Assert.AreEqual("4.0.1", index.Stable.Daemon!.Version);
        Assert.AreEqual("4.1.0-beta.1", index.Prerelease.Daemon!.Version);
    }

    [TestMethod]
    public void Other_train_lifts_to_stable()
    {
        var stale = new ChannelIndex
        {
            Stable = new Channel { Addin = new AddinEntry("4.1.0", new Uri(Downloads, "addin/v4.1.0/update-manifest.json")) },
            Prerelease = new Channel { Addin = new AddinEntry("4.1.0-rc.1", new Uri(Downloads, "addin/v4.1.0-rc.1/update-manifest.json")) }
        };

        var index = Channels.Advance(stale, Train.Daemon, "4.1.0", Downloads);

        Assert.AreEqual("4.1.0", index.Prerelease.Addin!.Version);
        Assert.AreEqual("4.1.0", index.Prerelease.Daemon!.Version);
    }

    [TestMethod]
    public void Refuses_nettest_train()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Channels.Advance(new ChannelIndex(), Train.NetTest, "0.3.0", Downloads));
    }

    [TestMethod]
    public void Refuses_non_release_version()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Channels.Advance(new ChannelIndex(), Train.Addin, "0.0.0-ci.5", Downloads));
    }

    [TestMethod]
    public void Checks_setup_and_manifest_urls()
    {
        var index = Advance(("daemon", "4.0.0"), ("addin", "4.0.0"));

        CollectionAssert.AreEquivalent(
            new[]
            {
                new Uri("https://github.com/o/r/releases/download/daemon/v4.0.0/RevitDevTool-Setup.exe"),
                new Uri("https://github.com/o/r/releases/download/addin/v4.0.0/update-manifest.json")
            },
            Channels.Urls(index).ToArray());
    }

    [TestMethod]
    public void Default_downloads_match_index()
    {
        Assert.AreEqual(new Uri("https://github.com/trgiangv/RevitDevTool/releases/download/"), Channels.Downloads);
    }

    private static ChannelIndex Advance(params (string Train, string Version)[] releases) =>
        releases.Aggregate(new ChannelIndex(), (index, release) => Channels.Advance(index, Train.Find(release.Train)!, release.Version, Downloads));
}

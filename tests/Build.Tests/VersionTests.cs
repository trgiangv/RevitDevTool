namespace Build.Tests;

[TestClass]
public sealed class VersionTests
{
    [TestMethod]
    [DataRow("4.0.0")]
    [DataRow("4.0.0-alpha.1")]
    [DataRow("4.0.0-beta.8")]
    [DataRow("0.3.0-rc.12")]
    public void SemVer_accepts_release_forms(string version)
    {
        Assert.IsNotNull(ReleaseVersion.Parse(version, VersionGrammar.SemVer));
    }

    [TestMethod]
    [DataRow("4.0.0-beta")]
    [DataRow("4.0.0-beta.0")]
    [DataRow("4.0.0-preview.1")]
    [DataRow("4.0.0b1")]
    [DataRow("04.0.0")]
    [DataRow("4.0")]
    [DataRow("4.0.0+abc")]
    public void SemVer_rejects_other_forms(string version)
    {
        Assert.IsNull(ReleaseVersion.Parse(version, VersionGrammar.SemVer));
    }

    [TestMethod]
    [DataRow("0.6.1")]
    [DataRow("0.8.0a1")]
    [DataRow("0.8.0b2")]
    [DataRow("0.8.0rc1")]
    public void Pep440_accepts_release_forms(string version)
    {
        Assert.IsNotNull(ReleaseVersion.Parse(version, VersionGrammar.Pep440));
    }

    [TestMethod]
    [DataRow("0.8.0-beta.1")]
    [DataRow("0.8.0.dev1")]
    [DataRow("0.8.0b0")]
    [DataRow("0.8")]
    public void Pep440_rejects_other_forms(string version)
    {
        Assert.IsNull(ReleaseVersion.Parse(version, VersionGrammar.Pep440));
    }

    [TestMethod]
    [DataRow("4.0.0-alpha.2", "4.0.0-beta.1", VersionGrammar.SemVer)]
    [DataRow("4.0.0-beta.9", "4.0.0-beta.10", VersionGrammar.SemVer)]
    [DataRow("4.0.0-beta.9", "4.0.0-rc.1", VersionGrammar.SemVer)]
    [DataRow("4.0.0-rc.3", "4.0.0", VersionGrammar.SemVer)]
    [DataRow("4.0.0", "4.0.1-alpha.1", VersionGrammar.SemVer)]
    [DataRow("0.8.0a3", "0.8.0b1", VersionGrammar.Pep440)]
    [DataRow("0.8.0rc1", "0.8.0", VersionGrammar.Pep440)]
    public void Labels_order_alpha_beta_rc(string lower, string higher, VersionGrammar grammar)
    {
        Assert.IsLessThan(0, ReleaseVersion.Parse(lower, grammar)!.CompareTo(ReleaseVersion.Parse(higher, grammar)));
    }

    [TestMethod]
    [DataRow("4.0.0", "5.0.0-beta.1", true)]
    [DataRow("4.0.0", "4.1.0", false)]
    [DataRow("0.2.0", "0.3.0", true)]
    [DataRow("0.2.0", "0.2.1", false)]
    public void Major_rule_uses_minor_under_zero(string previous, string next, bool raises)
    {
        var grammar = VersionGrammar.SemVer;
        Assert.AreEqual(raises, ReleaseVersion.Parse(next, grammar)!.RaisesMajorFrom(ReleaseVersion.Parse(previous, grammar)!));
    }

    [TestMethod]
    public void Previous_ignores_other_tags()
    {
        string[] tags = ["daemon/v9.0.0", "3.0.0", "v0.6.0", "addin/vnext", "addin/v4.0.0-beta.8"];

        Assert.AreEqual("4.0.0-beta.8", TrainVersion.PickPrevious(Train.Addin, tags));
    }

    [TestMethod]
    public void Previous_compares_by_version()
    {
        string[] tags = ["addin/v4.0.0-beta.10", "addin/v4.0.0-beta.9", "addin/v4.0.0-alpha.20"];

        Assert.AreEqual("4.0.0-beta.10", TrainVersion.PickPrevious(Train.Addin, tags));
    }

    [TestMethod]
    public void Previous_is_none_without_tags()
    {
        Assert.IsNull(TrainVersion.PickPrevious(Train.NetTest, ["daemon/v4.0.0-beta.8", "0.2.0"]));
    }

    [TestMethod]
    public void Delta_base_shares_channel()
    {
        string[] tags = ["daemon/v4.0.0", "daemon/v4.1.0-beta.1", "daemon/v4.1.0-beta.2", "daemon/v4.1.0-beta.3"];

        Assert.AreEqual("4.1.0-beta.2", TrainVersion.PickSameChannel(Train.Daemon, tags, "4.1.0-beta.3"));
        Assert.AreEqual("4.0.0", TrainVersion.PickSameChannel(Train.Daemon, tags, "4.0.1"));
    }

    [TestMethod]
    public void First_in_channel_has_no_base()
    {
        Assert.IsNull(TrainVersion.PickSameChannel(Train.Daemon, ["daemon/v4.0.0-beta.9"], "4.0.0"));
    }

    [TestMethod]
    [DataRow("daemon", "4.0.0", true)]
    [DataRow("daemon", "4.0.0-beta.8", false)]
    [DataRow("addin", "4.0.0", false)]
    public void Latest_only_for_stable_daemon(string train, string version, bool latest)
    {
        var parsed = ReleaseVersion.Parse(version, VersionGrammar.SemVer)!;

        Assert.AreEqual(latest, new TrainVersion(Train.Find(train)!, version, parsed.IsPrerelease, null).IsLatest);
    }

    [TestMethod]
    public void Ci_version_uses_run_number()
    {
        Assert.AreEqual("0.0.0-ci.42", TrainVersion.NonRelease(Train.Daemon, "42", DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void Local_version_uses_utc_stamp()
    {
        var now = new DateTimeOffset(2026, 11, 8, 9, 30, 5, TimeSpan.FromHours(7));

        Assert.AreEqual("0.0.0-local.20261108023005", TrainVersion.NonRelease(Train.NetTest, null, now));
    }

    [TestMethod]
    public void Pytest_version_is_dev()
    {
        Assert.AreEqual("0.0.0.dev42", TrainVersion.NonRelease(Train.PyTest, "42", DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void Reader_takes_first_version()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.props");
        File.WriteAllText(path, "<Project><PropertyGroup><Version /><Version>1.2.3</Version></PropertyGroup><PropertyGroup><Version>$(Other)</Version></PropertyGroup></Project>");
        try
        {
            Assert.AreEqual("1.2.3", VersionFile.Read(Path.GetDirectoryName(path)!, Path.GetFileName(path)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Reader_reads_pyproject_version()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.toml");
        File.WriteAllText(path, "[project]\nname = \"x\"\nversion = \"0.6.1\"\n\n[tool.x]\nversion = \"9\"\n");
        try
        {
            Assert.AreEqual("0.6.1", VersionFile.Read(Path.GetDirectoryName(path)!, Path.GetFileName(path)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Pack_replaces_first_version()
    {
        const string text = "[project]\nversion = \"0.6.1\"\n\n[tool.x]\nversion = \"9\"\n";

        Assert.AreEqual("[project]\nversion = \"0.0.0.dev42\"\n\n[tool.x]\nversion = \"9\"\n", VersionFile.WithTomlVersion(text, "0.0.0.dev42"));
    }
}

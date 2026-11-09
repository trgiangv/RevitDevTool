using Build.Pipeline;

namespace Build.Tests;

[TestClass]
public sealed class AddinHostsTests
{
    private string _root = null!;

    [TestInitialize]
    public void Create() => _root = Directory.CreateTempSubdirectory("addin-hosts").FullName;

    [TestCleanup]
    public void Delete() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public void Empty_hosts_are_refused()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => AddinHosts.CompiledVersion(_root));
    }

    [TestMethod]
    public void Mixed_compiles_are_refused()
    {
        Stage("2025", "1.0.0-ci.1");
        Stage("2026", "1.0.0-ci.2");

        Assert.ThrowsExactly<InvalidOperationException>(() => AddinHosts.CompiledVersion(_root));
    }

    [TestMethod]
    public void Missing_host_version_is_named()
    {
        Stage("2026", "1.0.0-ci.1");

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => AddinHosts.Require(_root, ["2025", "2026"], "1.0.0-ci.1"));

        StringAssert.Contains(exception.Message, "2025");
        Assert.IsFalse(exception.Message.Contains("2026"));
    }

    [TestMethod]
    public void Complete_hosts_resolve()
    {
        Stage("2025", "1.0.0-ci.1");
        Stage("2026", "1.0.0-ci.1");

        Assert.AreEqual("1.0.0-ci.1", AddinHosts.CompiledVersion(_root));
        Assert.HasCount(2, AddinHosts.Require(_root, ["2025", "2026"], "1.0.0-ci.1"));
    }

    private void Stage(string hostVersion, string version)
    {
        var folder = Directory.CreateDirectory(Path.Combine(AddinHosts.Folder(_root), hostVersion)).FullName;
        File.WriteAllText(folder + ".version", version);
    }
}

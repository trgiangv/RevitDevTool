namespace Build.Tests;

[TestClass]
public sealed class ReleaseRulesTests
{
    private const string Changelog = "# Changelog\n\n## [4.0.0]\n\n- First stable.\n";

    [TestMethod]
    public void First_beta_is_accepted()
    {
        Assert.IsEmpty(ReleaseRules.Refusals(new ReleaseInput(Train.Addin, "4.0.0-beta.8", null, null, "4.0.0-beta.8", null, ["4.0.0-beta.8"])));
    }

    [TestMethod]
    public void Refuses_addin_before_daemon()
    {
        AssertRefusal(
            new ReleaseInput(Train.Addin, "4.0.0-beta.8", null, null, "4.0.0-beta.8", null, ["4.0.0-beta.7"]),
            "Add-in 4.0.0-beta.8 needs a daemon release at or above MinUpdaterVersion 4.0.0-beta.8; release daemon first.");
    }

    [TestMethod]
    public void Stable_addin_needs_stable_daemon()
    {
        AssertRefusal(
            new ReleaseInput(Train.Addin, "4.0.0", "4.0.0-rc.1", Changelog, "4.0.0", [], ["4.1.0-beta.1"]),
            "Stable add-in 4.0.0 needs a stable daemon release at or above MinUpdaterVersion 4.0.0; release daemon first.");
    }

    [TestMethod]
    public void Refuses_zero_version()
    {
        AssertRefusal(
            new ReleaseInput(Train.NetTest, "0.0.0-local.20261108000000", null, null),
            "Version 0.0.0-local.20261108000000 is for local and CI builds only; set the release version in source/DevTools.TestAdapter/DevTools.TestAdapter.csproj.");
    }

    [TestMethod]
    public void Refuses_bad_grammar()
    {
        AssertRefusal(
            new ReleaseInput(Train.PyTest, "0.8.0-beta.1", null, null),
            "Version '0.8.0-beta.1' does not match MAJOR.MINOR.PATCH[aN|bN|rcN]; fix it in source/RevitDevTool.PyTest/pyproject.toml.");
    }

    [TestMethod]
    public void Refuses_version_not_greater()
    {
        AssertRefusal(
            new ReleaseInput(Train.Daemon, "4.0.0-beta.8", "4.0.0-beta.8", null),
            "Version 4.0.0-beta.8 is not greater than the latest tag daemon/v4.0.0-beta.8; raise it in source/DevTools.Daemon/DevTools.Daemon.csproj.");
    }

    [TestMethod]
    public void Refuses_label_going_back()
    {
        AssertRefusal(
            new ReleaseInput(Train.Daemon, "4.0.0-beta.9", "4.0.0-rc.1", null),
            "Version 4.0.0-beta.9 goes back from rc to beta; keep the label or raise the version in source/DevTools.Daemon/DevTools.Daemon.csproj.");
    }

    [TestMethod]
    public void Refuses_stable_without_section()
    {
        AssertRefusal(
            new ReleaseInput(Train.NetTest, "0.3.0", "0.2.0", "# Changelog\n\n## [0.2.0]\n"),
            "Stable version 0.3.0 needs a '## [0.3.0]' section in source/DevTools.TestAdapter/CHANGELOG.md.");
    }

    [TestMethod]
    public void Refuses_prerelease_min_updater()
    {
        AssertRefusal(
            new ReleaseInput(Train.Addin, "4.0.0", "4.0.0-rc.1", Changelog, "4.0.0-rc.1", []),
            "Stable add-in 4.0.0 needs a stable MinUpdaterVersion in source/AddinVersion.props, not '4.0.0-rc.1'.");
    }

    [TestMethod]
    public void Refuses_unshipped_api_lines()
    {
        AssertRefusal(
            new ReleaseInput(Train.Addin, "4.0.0", "4.0.0-rc.1", Changelog, "4.0.0", ["#nullable enable", "DevTools.Testing.Abstractions.Foo"]),
            "Stable add-in 4.0.0 needs an empty source/DevTools.Testing.Abstractions/PublicAPI.Unshipped.txt; move its lines to PublicAPI.Shipped.txt.");
    }

    [TestMethod]
    public void Stable_addin_with_section_passes()
    {
        Assert.IsEmpty(ReleaseRules.Refusals(new ReleaseInput(Train.Addin, "4.0.0", "4.0.0-rc.1", Changelog, "4.0.0", ["#nullable enable", ""], ["4.0.0"])));
    }

    [TestMethod]
    [DataRow("feat!: drop the old pipe", true)]
    [DataRow("refactor(ipc)!: rename", true)]
    [DataRow("fix: thing\n\nBREAKING CHANGE: removed x", true)]
    [DataRow("feat(ui): add a button", false)]
    public void Breaking_commit_is_detected(string message, bool breaking)
    {
        Assert.AreEqual(breaking, ReleaseRules.IsBreaking(message));
    }

    [TestMethod]
    [DataRow("source/DevTools.Execution/External/Testing/PytestProtocol.cs", "public static ProtocolRange Range { get; } = new(CurrentVersion, MinVersion);", true)]
    [DataRow("source/DevTools.Daemon/Tunnel/GatewayMessages.cs", "new(AppVersion.Current, new ProtocolRange(1, 1));", true)]
    [DataRow("props/TestingRuntime.props", "", true)]
    [DataRow("source/DevTools.Ipc/BridgePipeConnection.cs", "private readonly SemaphoreSlim _writeLock = new(1, 1);", false)]
    public void Contract_file_is_detected(string path, string content, bool contract)
    {
        Assert.AreEqual(contract, ReleaseRules.IsContract(path, content));
    }

    private static void AssertRefusal(ReleaseInput input, string expected)
    {
        CollectionAssert.Contains(ReleaseRules.Refusals(input).ToArray(), expected);
    }
}

using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Mcp.Search;
using DevTools.Ipc;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class ScoringTests
{
    [TestMethod]
    public void Score_TargetMatchDominates_AndHalfTokenCutoffApplies()
    {
        var hit = new CatalogItem(
            CatalogType.Tool,
            "read_file_info",
            "metadata",
            1,
            new InstanceInfo { HostApp = "Revit", ProcessId = 1, VersionNumber = "2025" });

        var oneToken = Scoring.Score(["read"], hit);
        Assert.AreEqual(4, oneToken);

        var twoTokensStrong = Scoring.Score(["read", "file"], hit);
        Assert.AreEqual(4, twoTokensStrong);

        var twoTokensWeak = Scoring.Score(["zzz", "aaa"], hit);
        Assert.AreEqual(0, twoTokensWeak);

        var described = new CatalogItem(
            CatalogType.Tool,
            "read_file_info",
            "alpha beta notes",
            1,
            new InstanceInfo { HostApp = "Revit", ProcessId = 1, VersionNumber = "2025" });
        var halfDescription = Scoring.Score(["alpha", "beta", "zzz", "qqq"], described);
        Assert.AreEqual(0.5, halfDescription);

        var belowHalf = Scoring.Score(["alpha", "zzz", "qqq", "yyy"], described);
        Assert.AreEqual(0, belowHalf);
    }
}

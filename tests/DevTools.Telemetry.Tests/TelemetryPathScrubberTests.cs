namespace DevTools.Telemetry.Tests;

[TestClass]
public sealed class TelemetryPathScrubberTests
{
    [TestMethod]
    public void Scrub_replaces_windows_absolute_path()
    {
        var input = "Failed at C:\\Users\\me\\project\\file.py line 1";
        var scrubbed = TelemetryPathScrubber.Scrub(input);
        Assert.DoesNotContain(":\\Users", scrubbed, StringComparison.Ordinal);
        Assert.Contains("[path]", scrubbed, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Scrub_empty_returns_empty()
    {
        Assert.AreEqual(string.Empty, TelemetryPathScrubber.Scrub(null));
        Assert.AreEqual(string.Empty, TelemetryPathScrubber.Scrub(""));
    }
}

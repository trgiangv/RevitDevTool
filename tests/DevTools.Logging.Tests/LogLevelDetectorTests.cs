using DevTools.Logging;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging.Tests;

[TestClass]
public sealed class LogLevelDetectorTests
{
    [TestMethod]
    [DataRow("[ERROR] boom", LogLevel.Error)]
    [DataRow("  [WARN] slow", LogLevel.Warning)]
    [DataRow("[INF] ok", LogLevel.Information)]
    [DataRow("[DBG] detail", LogLevel.Debug)]
    [DataRow("[TRACE] fine", LogLevel.Trace)]
    [DataRow("[FATAL] dead", LogLevel.Critical)]
    public void Detect_uses_bracket_prefixes(string message, LogLevel expected)
    {
        Assert.AreEqual(expected, LogLevelDetector.Detect(message, [], [], [], []));
    }

    [TestMethod]
    public void Detect_uses_custom_keywords_when_no_prefix()
    {
        Assert.AreEqual(
            LogLevel.Critical,
            LogLevelDetector.Detect("disk full", ["full"], [], [], []));
        Assert.AreEqual(
            LogLevel.Error,
            LogLevelDetector.Detect("request failed", [], ["failed"], [], []));
        Assert.AreEqual(
            LogLevel.Warning,
            LogLevelDetector.Detect("slow query", [], [], ["slow"], []));
        Assert.AreEqual(
            LogLevel.Information,
            LogLevelDetector.Detect("started", [], [], [], ["started"]));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ParseKeywords_returns_empty_for_blank_input(string? input)
    {
        Assert.IsEmpty(LogLevelDetector.ParseKeywords(input));
    }

    [TestMethod]
    public void ParseKeywords_trims_splits_and_lowercases_up_to_five()
    {
        var keywords = LogLevelDetector.ParseKeywords(" One, TWO , ,three,Four,FIVE,SIX ");
        Assert.AreSequenceEqual(["one", "two", "three", "four", "five"], keywords);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("a,b,c")]
    public void ValidateKeywords_allows_up_to_five_keywords(string? input)
    {
        Assert.IsNull(LogLevelDetector.ValidateKeywords(input));
    }

    [TestMethod]
    public void ValidateKeywords_rejects_more_than_five_keywords()
    {
        Assert.AreEqual(
            "Maximum 5 keywords allowed",
            LogLevelDetector.ValidateKeywords("a,b,c,d,e,f"));
    }
}

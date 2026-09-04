using DevTools.Logging;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging.Tests;

public sealed class LogLevelDetectorTests
{
    [Theory]
    [InlineData("[ERROR] boom", LogLevel.Error)]
    [InlineData("  [WARN] slow", LogLevel.Warning)]
    [InlineData("[INF] ok", LogLevel.Information)]
    [InlineData("[DBG] detail", LogLevel.Debug)]
    [InlineData("[TRACE] fine", LogLevel.Trace)]
    [InlineData("[FATAL] dead", LogLevel.Critical)]
    public void Detect_uses_bracket_prefixes(string message, LogLevel expected)
    {
        Assert.Equal(expected, LogLevelDetector.Detect(message, [], [], [], []));
    }

    [Fact]
    public void Detect_uses_custom_keywords_when_no_prefix()
    {
        Assert.Equal(
            LogLevel.Critical,
            LogLevelDetector.Detect("disk full", ["full"], [], [], []));
        Assert.Equal(
            LogLevel.Error,
            LogLevelDetector.Detect("request failed", [], ["failed"], [], []));
        Assert.Equal(
            LogLevel.Warning,
            LogLevelDetector.Detect("slow query", [], [], ["slow"], []));
        Assert.Equal(
            LogLevel.Information,
            LogLevelDetector.Detect("started", [], [], [], ["started"]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseKeywords_returns_empty_for_blank_input(string? input)
    {
        Assert.Empty(LogLevelDetector.ParseKeywords(input));
    }

    [Fact]
    public void ParseKeywords_trims_splits_and_lowercases_up_to_five()
    {
        var keywords = LogLevelDetector.ParseKeywords(" One, TWO , ,three,Four,FIVE,SIX ");
        Assert.Equal(["one", "two", "three", "four", "five"], keywords);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a,b,c")]
    public void ValidateKeywords_allows_up_to_five_keywords(string? input)
    {
        Assert.Null(LogLevelDetector.ValidateKeywords(input));
    }

    [Fact]
    public void ValidateKeywords_rejects_more_than_five_keywords()
    {
        Assert.Equal(
            "Maximum 5 keywords allowed",
            LogLevelDetector.ValidateKeywords("a,b,c,d,e,f"));
    }
}

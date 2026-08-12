using DevTools.NUnit.Runner.Services;

namespace DevTools.NUnit.Runner.Tests;

public sealed class NUnitRunnerFilterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_accepts_empty_filter(string? filter)
    {
        Assert.Null(NUnitRunnerFilter.Normalize(filter));
    }

    [Fact]
    public void Normalize_accepts_framework_filter_xml()
    {
        const string xml = "<filter><test>Sample.Fixture.Test</test></filter>";
        Assert.Equal(xml, NUnitRunnerFilter.Normalize(xml));
    }

    [Fact]
    public void Normalize_rejects_plain_tsl()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            NUnitRunnerFilter.Normalize("cat == 'Smoke'"));

        Assert.Contains("framework filter XML", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryNormalize_reports_plain_tsl_error()
    {
        var ok = NUnitRunnerFilter.TryNormalize("test == 'Name'", out _, out var error);

        Assert.False(ok);
        Assert.Contains("framework filter XML", error, StringComparison.Ordinal);
    }
}

using DevTools.TestRunner.Services;

namespace DevTools.TestRunner.Tests;

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

        Assert.Contains("Filter must be empty", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_from_method_name()
    {
        Assert.Equal(
            "<filter><name>Arithmetic_runs_inside_host</name></filter>",
            NUnitRunnerFilter.Compose(["Arithmetic_runs_inside_host"], [], xml: null));
    }

    [Fact]
    public void Compose_from_full_names_ors_and_escapes()
    {
        Assert.Equal(
            "<filter><or><test>A&lt;B&gt;</test><test>C</test></or></filter>",
            NUnitRunnerFilter.Compose([], ["A<B>", "C"], xml: null));
    }

    [Fact]
    public void Compose_rejects_xml_mixed_with_name()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            NUnitRunnerFilter.Compose(["A"], [], "<filter><test>B</test></filter>"));
        Assert.Equal(NUnitRunnerFilter.MixedFilterMessage, ex.Message);
    }
}

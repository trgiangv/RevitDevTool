using DevTools.NUnit.Host;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.Host.Tests;

public sealed class NUnitSelectionFilterTests
{
    [Fact]
    public void Empty_selection_runs_the_whole_assembly()
    {
        Assert.Null(NUnitSelectionFilter.ToNUnitFilter(null));
        Assert.Null(NUnitSelectionFilter.ToNUnitFilter(TestingSelection.All));
    }

    [Fact]
    public void TestIds_are_emitted_as_nunit_test_nodes_without_using_display_names()
    {
        var filter = NUnitSelectionFilter.ToNUnitFilter(
            TestingSelection.FromTestIds(
                ["DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes"]));

        Assert.Equal(
            "<filter><test>DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes</test></filter>",
            filter);
        Assert.DoesNotContain("<name>", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void Multiple_test_ids_are_or_combined()
    {
        var filter = NUnitSelectionFilter.ToNUnitFilter(
            TestingSelection.FromTestIds(["alpha", "beta"]));

        Assert.Equal("<filter><or><test>alpha</test><test>beta</test></or></filter>", filter);
    }

    [Fact]
    public void Names_are_emitted_as_nunit_name_nodes()
    {
        var filter = NUnitSelectionFilter.ToNUnitFilter(
            TestingSelection.FromNames(["Arithmetic_runs_inside_host"]));

        Assert.Equal("<filter><name re=\"1\">Arithmetic_runs_inside_host</name></filter>", filter);
    }

    [Fact]
    public void Provider_payload_is_raw_nunit_xml()
    {
        const string xml = "<filter><cat>AcceptanceCategory</cat></filter>";
        var filter = NUnitSelectionFilter.ToNUnitFilter(
            TestingSelection.FromFrameworkFilter(TestingSelection.XmlFilterFormat, xml));
        Assert.Equal(xml, filter);
    }

    [Fact]
    public void Mixed_ids_and_payload_are_rejected()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new TestingSelection(
                TestingSelectionKind.TestIds,
                testIds: ["id"],
                filterData: "<filter><test>id</test></filter>"));

        Assert.Contains("framework filter", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Wrong_filter_format_is_rejected_even_when_payload_is_xml()
    {
        var selection = TestingSelection.FromFrameworkFilter(
            "nunit/filter-xml",
            "<filter><test>x</test></filter>");
        var ex = Assert.Throws<ArgumentException>(() => NUnitSelectionFilter.ToNUnitFilter(selection));
        Assert.Contains(TestingSelection.XmlFilterFormat, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_xml_payload_is_rejected()
    {
        var selection = TestingSelection.FromFrameworkFilter(
            TestingSelection.XmlFilterFormat,
            "cat == AcceptanceCategory");
        Assert.Throws<ArgumentException>(() => NUnitSelectionFilter.ToNUnitFilter(selection));
    }
}

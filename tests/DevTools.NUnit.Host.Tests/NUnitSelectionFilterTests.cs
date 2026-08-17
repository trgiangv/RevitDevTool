using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.Host.Tests;

public sealed class NUnitSelectionFilterTests
{
    [Fact]
    public void Empty_selection_runs_the_whole_assembly()
    {
        Assert.Null(NUnitSelectionFilter.ToNUnitFilter(null));
        Assert.Null(NUnitSelectionFilter.ToNUnitFilter(new TestingSelection([], null)));
    }

    [Fact]
    public void TestIds_are_emitted_as_nunit_test_nodes_without_using_display_names()
    {
        var filter = NUnitSelectionFilter.ToNUnitFilter(
            new TestingSelection(
                ["DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes"],
                null));

        Assert.Equal(
            "<filter><test>DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes</test></filter>",
            filter);
        Assert.DoesNotContain("<name>", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void Multiple_test_ids_are_or_combined()
    {
        var filter = NUnitSelectionFilter.ToNUnitFilter(
            new TestingSelection(["alpha", "beta"], null));

        Assert.Equal("<filter><or><test>alpha</test><test>beta</test></or></filter>", filter);
    }

    [Fact]
    public void Provider_payload_is_raw_nunit_xml()
    {
        const string xml = "<filter><cat>AcceptanceCategory</cat></filter>";
        var filter = NUnitSelectionFilter.ToNUnitFilter(new TestingSelection([], xml));
        Assert.Equal(xml, filter);
    }

    [Fact]
    public void Mixed_ids_and_payload_are_rejected()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            NUnitSelectionFilter.ToNUnitFilter(
                new TestingSelection(["id"], "<filter><test>id</test></filter>")));

        Assert.StartsWith(NUnitSelectionFilter.MixedSelectionMessage, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_xml_payload_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            NUnitSelectionFilter.ToNUnitFilter(new TestingSelection([], "cat == AcceptanceCategory")));
    }
}

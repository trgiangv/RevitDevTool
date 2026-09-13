using DevTools.Testing.Host.NUnit;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.Host.Tests;

[TestClass]
public sealed class NUnitSelectionFilterTests
{
    [TestMethod]
    public void Empty_selection_runs_the_whole_assembly()
    {
        Assert.IsNull(NUnitSelectionFilter.ToNUnitFilter(null));
        Assert.IsNull(NUnitSelectionFilter.ToNUnitFilter(TestSelection.All));
    }

    [TestMethod]
    public void TestIds_are_emitted_as_nunit_test_nodes_without_using_display_names()
    {
        var filter = NUnitSelectionFilter.ToNUnitFilter(
            TestSelection.FromTestIds(
                ["DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes"]));

        Assert.AreEqual(
            "<filter><test>DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes</test></filter>",
            filter);
        Assert.DoesNotContain("<name>", filter!, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Multiple_test_ids_are_or_combined()
    {
        var filter = NUnitSelectionFilter.ToNUnitFilter(
            TestSelection.FromTestIds(["alpha", "beta"]));

        Assert.AreEqual("<filter><or><test>alpha</test><test>beta</test></or></filter>", filter);
    }

    [TestMethod]
    public void Names_are_emitted_as_nunit_name_nodes()
    {
        var filter = NUnitSelectionFilter.ToNUnitFilter(
            TestSelection.FromNames(["Arithmetic_runs_inside_host"]));

        Assert.AreEqual("<filter><name re=\"1\">Arithmetic_runs_inside_host</name></filter>", filter);
    }

    [TestMethod]
    public void Provider_payload_is_raw_nunit_xml()
    {
        const string xml = "<filter><cat>AcceptanceCategory</cat></filter>";
        var filter = NUnitSelectionFilter.ToNUnitFilter(
            TestSelection.FromFrameworkFilter(NUnitSelectionFilter.XmlFilterFormat, xml));
        Assert.AreEqual(xml, filter);
    }

    [TestMethod]
    public void Mixed_ids_and_payload_are_rejected()
    {
        var ex = Assert.ThrowsExactly<ArgumentException>(() =>
            new TestSelection(
                TestSelectionKind.TestIds,
                testIds: ["id"],
                filterData: "<filter><test>id</test></filter>"));

        Assert.Contains("framework filter", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Wrong_filter_format_is_rejected_even_when_payload_is_xml()
    {
        var selection = TestSelection.FromFrameworkFilter(
            "nunit/filter-xml",
            "<filter><test>x</test></filter>");
        var ex = Assert.ThrowsExactly<ArgumentException>(() => NUnitSelectionFilter.ToNUnitFilter(selection));
        Assert.Contains(NUnitSelectionFilter.XmlFilterFormat, ex.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Non_xml_payload_is_rejected()
    {
        var selection = TestSelection.FromFrameworkFilter(
            NUnitSelectionFilter.XmlFilterFormat,
            "cat == AcceptanceCategory");
        var ex = Assert.ThrowsExactly<ArgumentException>(() => NUnitSelectionFilter.ToNUnitFilter(selection));
        Assert.Contains("filter XML", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Xml_payload_without_filter_root_is_rejected()
    {
        var selection = TestSelection.FromFrameworkFilter(
            NUnitSelectionFilter.XmlFilterFormat,
            "<cat>AcceptanceCategory</cat>");
        Assert.ThrowsExactly<ArgumentException>(() => NUnitSelectionFilter.ToNUnitFilter(selection));
    }
}

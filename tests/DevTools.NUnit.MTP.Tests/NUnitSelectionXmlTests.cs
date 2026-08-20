using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.MTP.Tests;

public sealed class NUnitSelectionXmlTests
{
    [Fact]
    public void Names_become_nunit_name_regex_nodes()
    {
        var xml = NUnitSelectionXml.ToFilterXml(
            ["FamilyPolicy_GetAndList_DoNotLoadSampleRfa"]);

        Assert.Equal(
            "<filter><name re=\"1\">FamilyPolicy_GetAndList_DoNotLoadSampleRfa</name></filter>",
            xml);
    }

    [Fact]
    public void Test_ids_are_not_emitted_from_the_name_filter_helper()
    {
        var xml = NUnitSelectionXml.ToFilterXml(
            new TestingSelection(["DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes"]));

        Assert.Null(xml);
    }
}

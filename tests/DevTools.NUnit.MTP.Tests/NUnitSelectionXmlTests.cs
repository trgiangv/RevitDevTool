using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.MTP.Tests;

[TestClass]
public sealed class NUnitSelectionXmlTests
{
    [TestMethod]
    public void Names_become_nunit_name_regex_nodes()
    {
        var xml = NUnitSelectionXml.ToFilterXml(
            ["FamilyPolicy_GetAndList_DoNotLoadSampleRfa"]);

        Assert.AreEqual(
            "<filter><name re=\"1\">FamilyPolicy_GetAndList_DoNotLoadSampleRfa</name></filter>",
            xml);
    }

    [TestMethod]
    public void Test_ids_are_not_emitted_from_the_name_filter_helper()
    {
        var xml = NUnitSelectionXml.ToFilterXml(
            TestSelection.FromTestIds(["DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes"]));

        Assert.IsNull(xml);
    }
}

using DevTools.NUnit.MTP;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.MTP.Tests;

public sealed class NUnitSelectionXmlTests
{
    [Fact]
    public void Names_become_nunit_name_nodes()
    {
        var xml = NUnitSelectionXml.ToFilterXml(
            new TestingSelection([], Names: ["FamilyPolicy_GetAndList_DoNotLoadSampleRfa"]));

        Assert.Equal(
            "<filter><name>FamilyPolicy_GetAndList_DoNotLoadSampleRfa</name></filter>",
            xml);
    }

    [Fact]
    public void Test_ids_are_emitted_as_collapsed_nunit_nodes()
    {
        const string id = "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes";
        var xml = NUnitSelectionXml.ToFilterXml(new TestingSelection([id]));

        Assert.Contains($"<test>{id}</test>", xml, StringComparison.Ordinal);
        Assert.Contains("re=\"1\"", xml, StringComparison.Ordinal);
        Assert.Contains("<method>PlainTest_Passes</method>", xml, StringComparison.Ordinal);
    }
}

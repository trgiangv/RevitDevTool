using DevTools.NUnit.Runtime;
using NUnit.Framework.Internal;

namespace DevTools.NUnit.Runtime.Tests;

[TestClass]
public sealed class NUnitFilterFactoryTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_returns_empty_filter_for_blank_input(string? filterExpression)
    {
        var filter = NUnitFilterFactory.Create(filterExpression);
        Assert.AreSame(TestFilter.Empty, filter);
    }

    [TestMethod]
    public void Create_rejects_non_xml_payload()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => NUnitFilterFactory.Create("name==Smoke"));
        Assert.AreEqual("filterExpression", exception.ParamName);
    }

    [TestMethod]
    public void Create_parses_nunit_filter_xml()
    {
        var filter = NUnitFilterFactory.Create("<filter><name>Smoke</name></filter>");
        Assert.AreNotSame(TestFilter.Empty, filter);
    }
}

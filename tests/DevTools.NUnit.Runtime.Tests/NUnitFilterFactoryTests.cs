using DevTools.NUnit.Runtime;
using NUnit.Framework.Internal;

namespace DevTools.NUnit.Runtime.Tests;

public sealed class NUnitFilterFactoryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_returns_empty_filter_for_blank_input(string? filterExpression)
    {
        var filter = NUnitFilterFactory.Create(filterExpression);
        Assert.Same(TestFilter.Empty, filter);
    }

    [Fact]
    public void Create_rejects_non_xml_payload()
    {
        var exception = Assert.Throws<ArgumentException>(() => NUnitFilterFactory.Create("name==Smoke"));
        Assert.Equal("filterExpression", exception.ParamName);
    }

    [Fact]
    public void Create_parses_nunit_filter_xml()
    {
        var filter = NUnitFilterFactory.Create("<filter><name>Smoke</name></filter>");
        Assert.NotSame(TestFilter.Empty, filter);
    }
}

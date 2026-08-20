using DevTools.NUnit.Runtime;

namespace DevTools.NUnit.Runtime.Tests;

public sealed class NUnitTestNameParserTests
{
    [Fact]
    public void Split_keeps_method_when_fixture_has_constructor_arguments()
    {
        NUnitTestNameParser.Split(
            "DevTools.NUnit.SampleTests.NamedFixtureSourceTests(\"alpha.rvt\").Fixture_argument_is_preserved",
            out var className,
            out var methodName);

        Assert.Equal("DevTools.NUnit.SampleTests.NamedFixtureSourceTests", className);
        Assert.Equal("Fixture_argument_is_preserved", methodName);
    }

    [Fact]
    public void SplitIde_keeps_fixture_arguments_on_the_type()
    {
        NUnitTestNameParser.SplitIde(
            "DevTools.NUnit.SampleTests.NamedFixtureSourceTests(\"alpha.rvt\").Fixture_argument_is_preserved",
            out var ns,
            out var typeName,
            out var methodName);

        Assert.Equal("DevTools.NUnit.SampleTests", ns);
        Assert.Equal("NamedFixtureSourceTests(\"alpha.rvt\")", typeName);
        Assert.Equal("Fixture_argument_is_preserved", methodName);
    }

    [Fact]
    public void Split_strips_method_arguments_not_the_declaring_type()
    {
        NUnitTestNameParser.Split(
            "DevTools.NUnit.SampleTests.ValueSourceTests.Theory_values_are_combinatorial(0.0d,0.0d)",
            out var className,
            out var methodName);

        Assert.Equal("DevTools.NUnit.SampleTests.ValueSourceTests", className);
        Assert.Equal("Theory_values_are_combinatorial", methodName);
    }

    [Fact]
    public void ToIdeTestId_keeps_ordinary_parameterized_full_name()
    {
        const string fullName =
            "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.TestCase_Addition(1,1,2)";
        Assert.Equal(
            fullName,
            NUnitTestNameParser.ToIdeTestId(
                fullName,
                "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture",
                "TestCase_Addition",
                "TestCase_Addition(1,1,2)"));
    }

    [Fact]
    public void ToIdeTestId_maps_testname_leaf_onto_csharp_method()
    {
        Assert.Equal(
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named(\"Named_one\")",
            NUnitTestNameParser.ToIdeTestId(
                "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one",
                "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture",
                "Original_named",
                "Named_one"));
    }

    [Fact]
    public void ToIdeTestId_keeps_generic_method_full_name()
    {
        const string fullName = "DevTools.NUnit.SampleTests.GenericClosedTests.M<Int32>(1)";
        Assert.Equal(
            fullName,
            NUnitTestNameParser.ToIdeTestId(
                fullName,
                "DevTools.NUnit.SampleTests.GenericClosedTests",
                "M",
                "M<Int32>(1)"));
    }
}

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

    [Fact]
    public void ToMetadataTypeName_uses_backtick_arity()
    {
        Assert.Equal(
            "DevTools.NUnit.SampleTests.GenericClosedTests`1",
            NUnitTestNameParser.ToMetadataTypeName(
                "DevTools.NUnit.SampleTests.GenericClosedTests<Int32>"));
        Assert.Equal(
            "Outer`1+Inner",
            NUnitTestNameParser.ToMetadataTypeName("Outer<Int32>+Inner"));
        Assert.Equal(
            "Dictionary`2",
            NUnitTestNameParser.ToMetadataTypeName("Dictionary<String,Int32>"));
    }

    [Fact]
    public void ToMetadataTypeSegment_strips_namespace_and_display_args()
    {
        Assert.Equal(
            "GenericClosedTests`1",
            NUnitTestNameParser.ToMetadataTypeSegment(
                "DevTools.NUnit.SampleTests.GenericClosedTests<Int32>"));
        Assert.Equal(
            "NamedFixtureSourceTests",
            NUnitTestNameParser.ToMetadataTypeSegment(
                "NamedFixtureSourceTests(\"alpha.rvt\")"));
    }

    [Fact]
    public void ToSourceTypeSegment_strips_backtick_arity_for_ide_bind()
    {
        Assert.Equal(
            "GenericClosedTests",
            NUnitTestNameParser.ToSourceTypeSegment(
                "DevTools.NUnit.SampleTests.GenericClosedTests<Int32>"));
        Assert.Equal(
            "Outer+Inner",
            NUnitTestNameParser.ToSourceTypeSegment("Outer<Int32>+Inner"));
        Assert.Equal(
            "NamedFixtureSourceTests",
            NUnitTestNameParser.ToSourceTypeSegment(
                "NamedFixtureSourceTests(\"alpha.rvt\")"));
    }

    [Fact]
    public void AppendDisplayArguments_copies_fixture_constructor_args_not_generic_args()
    {
        Assert.Equal(
            "Fixture_argument_is_preserved(\"alpha.rvt\")",
            NUnitTestNameParser.AppendDisplayArguments(
                "Fixture_argument_is_preserved",
                "NamedFixtureSourceTests(\"alpha.rvt\")"));
        Assert.Equal(
            "Generic_int_fixture_is_discovered",
            NUnitTestNameParser.AppendDisplayArguments(
                "Generic_int_fixture_is_discovered",
                "GenericClosedTests<Int32>"));
        Assert.Equal(
            "Method(\"x\")",
            NUnitTestNameParser.AppendDisplayArguments(
                "Method",
                "GenericClosedTests<Int32>(\"x\")"));
    }
}

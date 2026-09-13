using DevTools.NUnit.Runtime;

namespace DevTools.NUnit.Runtime.Tests;

[TestClass]
public sealed class NUnitTestNameParserTests
{
    [TestMethod]
    public void Split_keeps_method_when_fixture_has_constructor_arguments()
    {
        NUnitTestNameParser.Split(
            "DevTools.NUnit.SampleTests.NamedFixtureSourceTests(\"alpha.rvt\").Fixture_argument_is_preserved",
            out var className,
            out var methodName);

        Assert.AreEqual("DevTools.NUnit.SampleTests.NamedFixtureSourceTests", className);
        Assert.AreEqual("Fixture_argument_is_preserved", methodName);
    }

    [TestMethod]
    public void SplitIde_keeps_fixture_arguments_on_the_type()
    {
        NUnitTestNameParser.SplitIde(
            "DevTools.NUnit.SampleTests.NamedFixtureSourceTests(\"alpha.rvt\").Fixture_argument_is_preserved",
            out var className,
            out var ns,
            out var typeName,
            out var methodName);

        Assert.AreEqual(
            "DevTools.NUnit.SampleTests.NamedFixtureSourceTests(\"alpha.rvt\")",
            className);
        Assert.AreEqual("DevTools.NUnit.SampleTests", ns);
        Assert.AreEqual("NamedFixtureSourceTests(\"alpha.rvt\")", typeName);
        Assert.AreEqual("Fixture_argument_is_preserved", methodName);
    }

    [TestMethod]
    public void Split_strips_method_arguments_not_the_declaring_type()
    {
        NUnitTestNameParser.Split(
            "DevTools.NUnit.SampleTests.ValueSourceTests.Theory_values_are_combinatorial(0.0d,0.0d)",
            out var className,
            out var methodName);

        Assert.AreEqual("DevTools.NUnit.SampleTests.ValueSourceTests", className);
        Assert.AreEqual("Theory_values_are_combinatorial", methodName);
    }

    [TestMethod]
    public void ToIdeTestId_keeps_ordinary_parameterized_full_name()
    {
        const string fullName =
            "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.TestCase_Addition(1,1,2)";
        Assert.AreEqual(
            fullName,
            NUnitTestNameParser.ToIdeTestId(
                fullName,
                "DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture",
                "TestCase_Addition",
                "TestCase_Addition(1,1,2)"));
    }

    [TestMethod]
    public void ToIdeTestId_maps_testname_leaf_onto_csharp_method()
    {
        Assert.AreEqual(
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named(\"Named_one\")",
            NUnitTestNameParser.ToIdeTestId(
                "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one",
                "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture",
                "Original_named",
                "Named_one"));
    }

    [TestMethod]
    public void ToIdeTestId_keeps_generic_method_full_name()
    {
        const string fullName = "DevTools.NUnit.SampleTests.GenericClosedTests.M<Int32>(1)";
        Assert.AreEqual(
            fullName,
            NUnitTestNameParser.ToIdeTestId(
                fullName,
                "DevTools.NUnit.SampleTests.GenericClosedTests",
                "M",
                "M<Int32>(1)"));
    }

    [TestMethod]
    public void ToMetadataTypeName_uses_backtick_arity()
    {
        Assert.AreEqual(
            "DevTools.NUnit.SampleTests.GenericClosedTests`1",
            NUnitTestNameParser.ToMetadataTypeName(
                "DevTools.NUnit.SampleTests.GenericClosedTests<Int32>"));
        Assert.AreEqual(
            "Outer`1+Inner",
            NUnitTestNameParser.ToMetadataTypeName("Outer<Int32>+Inner"));
        Assert.AreEqual(
            "Dictionary`2",
            NUnitTestNameParser.ToMetadataTypeName("Dictionary<String,Int32>"));
    }

    [TestMethod]
    public void ToMetadataTypeSegment_strips_namespace_and_display_args()
    {
        Assert.AreEqual(
            "GenericClosedTests`1",
            NUnitTestNameParser.ToMetadataTypeSegment(
                "DevTools.NUnit.SampleTests.GenericClosedTests<Int32>"));
        Assert.AreEqual(
            "NamedFixtureSourceTests",
            NUnitTestNameParser.ToMetadataTypeSegment(
                "NamedFixtureSourceTests(\"alpha.rvt\")"));
    }

    [TestMethod]
    public void ToSourceTypeSegment_strips_backtick_arity_for_ide_bind()
    {
        Assert.AreEqual(
            "GenericClosedTests",
            NUnitTestNameParser.ToSourceTypeSegment(
                "DevTools.NUnit.SampleTests.GenericClosedTests<Int32>"));
        Assert.AreEqual(
            "Outer+Inner",
            NUnitTestNameParser.ToSourceTypeSegment("Outer<Int32>+Inner"));
        Assert.AreEqual(
            "NamedFixtureSourceTests",
            NUnitTestNameParser.ToSourceTypeSegment(
                "NamedFixtureSourceTests(\"alpha.rvt\")"));
    }

    [TestMethod]
    public void AppendDisplayArguments_copies_fixture_constructor_args_not_generic_args()
    {
        Assert.AreEqual(
            "Fixture_argument_is_preserved(\"alpha.rvt\")",
            NUnitTestNameParser.AppendDisplayArguments(
                "Fixture_argument_is_preserved",
                "NamedFixtureSourceTests(\"alpha.rvt\")"));
        Assert.AreEqual(
            "Generic_int_fixture_is_discovered",
            NUnitTestNameParser.AppendDisplayArguments(
                "Generic_int_fixture_is_discovered",
                "GenericClosedTests<Int32>"));
        Assert.AreEqual(
            "Method(\"x\")",
            NUnitTestNameParser.AppendDisplayArguments(
                "Method",
                "GenericClosedTests<Int32>(\"x\")"));
    }

    [TestMethod]
    public void SplitIde_last_dot_ignores_dots_inside_parens_and_closed_generics()
    {
        NUnitTestNameParser.SplitIde(
            "DevTools.NUnit.SampleTests.GenericClosedTests<Int32>.Generic_int_fixture_is_discovered",
            out var className,
            out var ns,
            out var typeName,
            out var methodName);

        Assert.AreEqual("DevTools.NUnit.SampleTests.GenericClosedTests<Int32>", className);
        Assert.AreEqual("DevTools.NUnit.SampleTests", ns);
        Assert.AreEqual("GenericClosedTests<Int32>", typeName);
        Assert.AreEqual("Generic_int_fixture_is_discovered", methodName);
    }

    [TestMethod]
    public void GroupKey_strips_argument_lists_and_empty_parens()
    {
        Assert.AreEqual(
            "Ns.Box.Bottom_corners_share_min_z",
            NUnitTestNameParser.GroupKey(
                "Ns.Box.Bottom_corners_share_min_z(-12.3d,45.6d,-7.8d,34.5d,67.8d,12.3d)"));
        Assert.AreEqual(
            "Ns.Box.Bottom_corners_share_min_z",
            NUnitTestNameParser.GroupKey("Ns.Box.Bottom_corners_share_min_z()"));
    }

    [TestMethod]
    public void GroupKey_uses_csharp_method_when_testname_renamed_the_leaf()
    {
        Assert.AreEqual(
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named",
            NUnitTestNameParser.GroupKey(
                "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one",
                "Original_named"));
        Assert.AreEqual(
            "Ns.Box.Bottom_corners_share_min_z",
            NUnitTestNameParser.GroupKey(
                "Ns.Box.Bottom_corners_share_min_z(-12.3,45.6)",
                "Bottom_corners_share_min_z",
                "Box",
                "Ns"));
    }
}

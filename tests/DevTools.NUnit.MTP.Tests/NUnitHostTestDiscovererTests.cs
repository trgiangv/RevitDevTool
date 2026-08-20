using DevTools.NUnit.MTP;
using DevTools.NUnit.Runtime.Fixtures;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.MTP.Tests;

public sealed class NUnitHostTestDiscovererTests
{
    [Fact]
    public void Discover_emits_parameterized_leaves_with_nunit_full_names()
    {
        var discoverer = new NUnitHostTestDiscoverer();
        var cases = discoverer.Discover(FixturePath);

        var additions = cases
            .Where(test => test.DisplayName.StartsWith("TestCase_Addition", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(3, additions.Count);
        Assert.All(additions, test =>
        {
            Assert.Equal(test.FullName, test.TestId);
            Assert.Contains("FullSemanticsFixture.TestCase_Addition", test.TestId, StringComparison.Ordinal);
        });

        var sources = cases
            .Where(test => test.TestId.Contains("TestCaseSource_StaticProvider", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(3, sources.Count);
    }

    [Fact]
    public void Select_name_uses_nunit_name_regex_not_method_identity()
    {
        var discoverer = new NUnitHostTestDiscoverer();
        var selected = discoverer.Select(
            FixturePath,
            new TestingSelection([], Names: ["TestCase_Addition"]));

        Assert.Equal(3, selected.Count);
        Assert.All(selected, test =>
            Assert.StartsWith("TestCase_Addition", test.DisplayName, StringComparison.Ordinal));
    }

    [Fact]
    public void Select_test_id_matches_nunit_full_name()
    {
        var discoverer = new NUnitHostTestDiscoverer();
        var all = discoverer.Discover(FixturePath);
        var one = all.First(test => test.DisplayName.StartsWith("TestCase_Addition", StringComparison.Ordinal));

        var selected = discoverer.Select(
            FixturePath,
            new TestingSelection([one.TestId]));

        Assert.Equal(one.TestId, Assert.Single(selected).TestId);
    }

    [Fact]
    public void Discover_testname_uid_keeps_csharp_method_in_the_fqn()
    {
        var named = new NUnitHostTestDiscoverer().Discover(FixturePath)
            .Single(test => test.DisplayName == "Named_one");

        Assert.Equal("Original_named", named.MethodName);
        Assert.Equal(
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named(\"Named_one\")",
            named.TestId);
        Assert.Equal(
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one",
            named.FullName);
    }

    [Fact]
    public void Select_display_name_is_not_a_test_id()
    {
        var discoverer = new NUnitHostTestDiscoverer();
        var selected = discoverer.Select(
            FixturePath,
            new TestingSelection(["PlainTest_Passes"]));

        Assert.Empty(selected);
    }

    [Fact]
    public void Discover_groups_fixture_source_instances_under_parameterized_type_names()
    {
        var discoverer = new NUnitHostTestDiscoverer();
        var cases = discoverer.Discover(FixturePath)
            .Where(test => test.MethodName == "FixtureSource_ValueIsPreserved")
            .ToList();

        Assert.Equal(2, cases.Count);
        Assert.All(cases, test =>
        {
            Assert.Equal(test.FullName, test.TestId);
            Assert.Equal("FixtureSource_ValueIsPreserved", test.MethodName);
            Assert.Contains("ParameterizedFixture(", test.ClassName, StringComparison.Ordinal);
            Assert.StartsWith("DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture(", test.ClassName, StringComparison.Ordinal);
        });
        Assert.Equal(2, cases.Select(test => test.ClassName).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Discover_fixture_source_uids_include_constructor_arguments()
    {
        var discoverer = new NUnitHostTestDiscoverer();
        var cases = discoverer.Discover(FixturePath)
            .Where(test => test.MethodName == "FixtureSource_ValueIsPreserved")
            .ToList();

        Assert.Equal(2, cases.Count);
        Assert.DoesNotContain(
            cases,
            test => test.TestId.Equals(
                "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture.FixtureSource_ValueIsPreserved",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Discover_attaches_pdb_source_for_a_plain_test()
    {
        var discoverer = new NUnitHostTestDiscoverer();
        var plain = discoverer.Discover(FixturePath)
            .Single(test => test.DisplayName == "PlainTest_Passes");

        Assert.NotNull(plain.Source);
        Assert.Contains("FullSemanticsFixture.cs", plain.Source!.File, StringComparison.OrdinalIgnoreCase);
        Assert.True(plain.Source.Line > 0);
    }

    static string FixturePath
    {
        get
        {
            var path = typeof(FullSemanticsFixture).Assembly.Location;
            Assert.False(string.IsNullOrWhiteSpace(path));
            return path;
        }
    }
}

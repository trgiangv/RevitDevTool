using DevTools.NUnit.MTP;
using DevTools.NUnit.Runtime.Fixtures;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.MTP.Tests;

[TestClass]
public sealed class NUnitTestDiscovererTests
{
    [TestMethod]
    public void Discover_emits_parameterized_leaves_with_nunit_full_names()
    {
        var discoverer = new NUnitTestDiscoverer();
        var cases = discoverer.Discover(FixturePath, TestSelection.All);

        var additions = cases
            .Where(test => test.DisplayName.StartsWith("TestCase_Addition", StringComparison.Ordinal))
            .ToList();
        Assert.AreEqual(3, additions.Count);
        foreach (var test in additions)
        {
            Assert.AreEqual(test.FullName, test.TestId);
            Assert.Contains("FullSemanticsFixture.TestCase_Addition", test.TestId, StringComparison.Ordinal);
        }

        var sources = cases
            .Where(test => test.TestId.Contains("TestCaseSource_StaticProvider", StringComparison.Ordinal))
            .ToList();
        Assert.AreEqual(3, sources.Count);
    }

    [TestMethod]
    public void Discover_name_uses_nunit_name_regex_not_method_identity()
    {
        var discoverer = new NUnitTestDiscoverer();
        var selected = discoverer.Discover(
            FixturePath,
            TestSelection.FromNames(["TestCase_Addition"]));

        Assert.AreEqual(3, selected.Count);
        Assert.IsTrue(selected.All(test => test.DisplayName.StartsWith("TestCase_Addition", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Discover_test_id_matches_nunit_full_name()
    {
        var discoverer = new NUnitTestDiscoverer();
        var all = discoverer.Discover(FixturePath, TestSelection.All);
        var one = all.First(test => test.DisplayName.StartsWith("TestCase_Addition", StringComparison.Ordinal));

        var selected = discoverer.Discover(
            FixturePath,
            TestSelection.FromTestIds([one.TestId]));

        Assert.AreEqual(one.TestId, selected.Single().TestId);
    }

    [TestMethod]
    public void Discover_empty_parens_method_uid_selects_parameterized_leaves()
    {
        var discoverer = new NUnitTestDiscoverer();
        var all = discoverer.Discover(FixturePath, TestSelection.All);
        var one = all.First(test => test.DisplayName.StartsWith("TestCase_Addition", StringComparison.Ordinal));
        var methodUid = one.TestId[..one.TestId.IndexOf('(')] + "()";

        var selected = discoverer.Discover(
            FixturePath,
            TestSelection.FromTestIds([methodUid]));

        Assert.AreEqual(3, selected.Count);
        Assert.IsTrue(selected.All(test => test.DisplayName.StartsWith("TestCase_Addition", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Discover_testname_uid_keeps_csharp_method_in_the_fqn()
    {
        var named = new NUnitTestDiscoverer().Discover(FixturePath, TestSelection.All)
            .Single(test => test.DisplayName == "Named_one");

        Assert.AreEqual("Original_named", named.MethodName);
        Assert.AreEqual(
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named(\"Named_one\")",
            named.TestId);
        Assert.AreEqual(
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one",
            named.FullName);
    }

    [TestMethod]
    public void Discover_display_name_is_not_a_test_id()
    {
        var discoverer = new NUnitTestDiscoverer();
        var selected = discoverer.Discover(
            FixturePath,
            TestSelection.FromTestIds(["PlainTest_Passes"]));

        Assert.IsEmpty(selected);
    }

    [TestMethod]
    public void Discover_display_name_with_args_selects_the_leaf()
    {
        var discoverer = new NUnitTestDiscoverer();
        var all = discoverer.Discover(FixturePath, TestSelection.All);
        var one = all.First(test => test.DisplayName.StartsWith("TestCase_Addition", StringComparison.Ordinal));

        var selected = discoverer.Discover(
            FixturePath,
            TestSelection.FromTestIds([one.DisplayName]));

        Assert.AreEqual(one.TestId, selected.Single().TestId);
    }

    [TestMethod]
    public void Discover_groups_fixture_source_instances_under_parameterized_type_names()
    {
        var discoverer = new NUnitTestDiscoverer();
        var cases = discoverer.Discover(FixturePath, TestSelection.All)
            .Where(test => test.MethodName == "FixtureSource_ValueIsPreserved")
            .ToList();

        Assert.AreEqual(2, cases.Count);
        foreach (var test in cases)
        {
            Assert.AreEqual(test.FullName, test.TestId);
            Assert.AreEqual("FixtureSource_ValueIsPreserved", test.MethodName);
            Assert.Contains("ParameterizedFixture(", test.ClassName!, StringComparison.Ordinal);
            Assert.IsTrue(test.ClassName!.StartsWith("DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture(", StringComparison.Ordinal));
        }
        Assert.AreEqual(2, cases.Select(test => test.ClassName).Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    public void Discover_fixture_source_uids_include_constructor_arguments()
    {
        var discoverer = new NUnitTestDiscoverer();
        var cases = discoverer.Discover(FixturePath, TestSelection.All)
            .Where(test => test.MethodName == "FixtureSource_ValueIsPreserved")
            .ToList();

        Assert.AreEqual(2, cases.Count);
        Assert.IsFalse(
            cases.Any(test  => test.TestId.Equals(
                "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture.FixtureSource_ValueIsPreserved",
                StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Discover_attaches_pdb_source_for_a_plain_test()
    {
        var discoverer = new NUnitTestDiscoverer();
        var plain = discoverer.Discover(FixturePath, TestSelection.All)
            .Single(test => test.DisplayName == "PlainTest_Passes");

        Assert.IsNotNull(plain.Source);
        Assert.Contains("FullSemanticsFixture.cs", plain.Source!.File, StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(plain.Source.Line > 0);
    }

    [TestMethod]
    public void Discover_attaches_pdb_source_for_a_generic_fixture()
    {
        var discoverer = new NUnitTestDiscoverer();
        var generic = discoverer.Discover(FixturePath, TestSelection.All)
            .First(test => test.MethodName == "GenericFixture_UsesRequestedType");

        Assert.IsNotNull(generic.Source);
        Assert.Contains("ParameterizedFixture.cs", generic.Source!.File, StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(generic.Source.Line > 0);
        Assert.Contains("GenericFixture<", generic.ClassName!, StringComparison.Ordinal);
        Assert.AreEqual("DevTools.NUnit.Runtime.Fixtures", generic.Namespace);
        Assert.AreEqual("GenericFixture", generic.TypeName);
        Assert.AreEqual("GenericFixture_UsesRequestedType", generic.DisplayName);
    }

    [TestMethod]
    public void Discover_fixture_source_display_name_keeps_constructor_arguments()
    {
        var cases = new NUnitTestDiscoverer().Discover(FixturePath, TestSelection.All)
            .Where(test => test.MethodName == "FixtureSource_ValueIsPreserved")
            .ToList();

        Assert.AreEqual(2, cases.Count);
        foreach (var test in cases)
        {
            Assert.AreEqual("ParameterizedFixture", test.TypeName);
            Assert.AreEqual("DevTools.NUnit.Runtime.Fixtures", test.Namespace);
            Assert.Contains("(", test.DisplayName, StringComparison.Ordinal);
        }
    }

    static string FixturePath
    {
        get
        {
            var path = typeof(FullSemanticsFixture).Assembly.Location;
            Assert.IsFalse(string.IsNullOrWhiteSpace(path));
            return path;
        }
    }
}

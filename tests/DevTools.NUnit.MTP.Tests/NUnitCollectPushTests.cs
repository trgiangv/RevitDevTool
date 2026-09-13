using DevTools.NUnit.MTP;
using DevTools.NUnit.Runtime;
using DevTools.NUnit.Runtime.Fixtures;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.MTP.Tests;

/// <summary>
/// Independent collect → filter XML push. No Test Explorer, no host.
/// Testhost ExploreTests of a throwing TestFixtureSource is one NotRunnable
/// leaf whose UID is Class.Method (no constructor args). Collapsed filter
/// XML also selects in-host Class("args").Method and TestName/SetName leaves.
/// </summary>
[TestClass]
public sealed class NUnitCollectPushTests
{
    [TestMethod]
    public void Collect_emits_collapsed_source_stub_full_name()
    {
        var stub = CollectStub();

        Assert.AreEqual(stub.FullName, stub.TestId);
        Assert.AreEqual("Stub_leaf", stub.MethodName);
        Assert.Contains("CollapsedSourceStubFixture", stub.TestId, StringComparison.Ordinal);
        Assert.DoesNotContain("CollapsedSourceStubFixture(", stub.TestId, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Discover_by_collected_uid_returns_the_same_leaf()
    {
        var stub = CollectStub();
        var discoverer = new NUnitTestDiscoverer();
        var selected = discoverer.Discover(FixturePath, TestSelection.FromTestIds([stub.TestId]));

        Assert.AreEqual(stub.TestId, selected.Single().TestId);
    }

    [TestMethod]
    public void Display_name_is_not_a_uid_so_discover_misses()
    {
        var discoverer = new NUnitTestDiscoverer();
        var selected = discoverer.Discover(FixturePath, TestSelection.FromTestIds(["Stub_leaf"]));

        Assert.IsEmpty(selected);
    }

    [TestMethod]
    public void Push_xml_is_collapsed_addtest_full_name()
    {
        var stub = CollectStub();
        var xml = NUnitCollapsedSelection.ToFilterXml([stub.TestId]);

        Assert.Contains($"<test>{stub.TestId}</test>", xml!, StringComparison.Ordinal);
        Assert.Contains("re=\"1\"", xml!, StringComparison.Ordinal);
        Assert.Contains("<method>Stub_leaf</method>", xml!, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Pushed_method_fqn_matches_expanded_fixture_source_leaves()
    {
        var stubId =
            "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture.FixtureSource_ValueIsPreserved";
        var xml = NUnitCollapsedSelection.ToFilterXml([stubId]);
        var filter = NUnitFilterFactory.Create(xml);

        using var session = NUnitLocalExploration.Load(FixturePath);
        var expanded = session.Leaves
            .Where(test => test.MethodName == "FixtureSource_ValueIsPreserved")
            .ToList();

        Assert.AreEqual(2, expanded.Count);
        foreach (var test in expanded)
        {
            Assert.IsTrue(filter.Pass(test));
            Assert.Contains("ParameterizedFixture(", test.FullName!, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void Discover_by_method_fqn_finds_testname_leaves()
    {
        const string methodId = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named";
        var discoverer = new NUnitTestDiscoverer();
        var selected = discoverer.Discover(FixturePath, TestSelection.FromTestIds([methodId]));

        Assert.AreEqual(2, selected.Count);
        Assert.AreSequenceEqual(
            ["Named_one", "Named_two"],
            selected.Select(test => test.DisplayName).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.IsTrue(selected.All(test => test.MethodName == "Original_named"));
        Assert.IsTrue(selected.All(test => test.TestId.StartsWith(
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named(\"",
            StringComparison.Ordinal)));
        Assert.IsTrue(selected.All(test => test.FullName!.Contains(
            "TestNameCaseFixture.Named_",
            StringComparison.Ordinal)));
    }

    static TestDiscoveredTest CollectStub()
    {
        var discoverer = new NUnitTestDiscoverer();
        return discoverer.Discover(FixturePath, TestSelection.All)
            .Single(test => test.MethodName == "Stub_leaf"
                && test.TestId.Contains("CollapsedSourceStubFixture", StringComparison.Ordinal));
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

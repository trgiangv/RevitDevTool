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
public sealed class NUnitCollectPushTests
{
    [Fact]
    public void Collect_emits_collapsed_source_stub_full_name()
    {
        var stub = CollectStub();

        Assert.Equal(stub.FullName, stub.TestId);
        Assert.Equal("Stub_leaf", stub.MethodName);
        Assert.Contains("CollapsedSourceStubFixture", stub.TestId, StringComparison.Ordinal);
        Assert.DoesNotContain("CollapsedSourceStubFixture(", stub.TestId, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_by_collected_uid_returns_the_same_leaf()
    {
        var stub = CollectStub();
        var discoverer = new NUnitHostTestDiscoverer();
        var selected = discoverer.Select(FixturePath, new TestingSelection([stub.TestId]));

        Assert.Equal(stub.TestId, Assert.Single(selected).TestId);
    }

    [Fact]
    public void Display_name_is_not_a_uid_so_select_misses()
    {
        var discoverer = new NUnitHostTestDiscoverer();
        var selected = discoverer.Select(FixturePath, new TestingSelection(["Stub_leaf"]));

        Assert.Empty(selected);
    }

    [Fact]
    public void Push_xml_is_collapsed_addtest_full_name()
    {
        var stub = CollectStub();
        var xml = NUnitCollapsedSelection.ToFilterXml([stub.TestId]);

        Assert.Contains($"<test>{stub.TestId}</test>", xml, StringComparison.Ordinal);
        Assert.Contains("re=\"1\"", xml, StringComparison.Ordinal);
        Assert.Contains("<method>Stub_leaf</method>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Pushed_method_fqn_matches_expanded_fixture_source_leaves()
    {
        var stubId =
            "DevTools.NUnit.Runtime.Fixtures.ParameterizedFixture.FixtureSource_ValueIsPreserved";
        var xml = NUnitCollapsedSelection.ToFilterXml([stubId]);
        var filter = NUnitFilterXml.Create(xml);

        using var session = NUnitLocalExploration.Load(FixturePath);
        var expanded = session.Leaves
            .Where(test => test.MethodName == "FixtureSource_ValueIsPreserved")
            .ToList();

        Assert.Equal(2, expanded.Count);
        Assert.All(expanded, test => Assert.True(filter.Pass(test)));
        Assert.All(
            expanded,
            test => Assert.Contains("ParameterizedFixture(", test.FullName, StringComparison.Ordinal));
    }

    [Fact]
    public void Select_by_method_fqn_finds_testname_leaves()
    {
        const string methodId = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named";
        var discoverer = new NUnitHostTestDiscoverer();
        var selected = discoverer.Select(FixturePath, new TestingSelection([methodId]));

        Assert.Equal(2, selected.Count);
        Assert.Equal(
            ["Named_one", "Named_two"],
            selected.Select(test => test.DisplayName).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.All(selected, test => Assert.Equal("Original_named", test.MethodName));
        Assert.All(
            selected,
            test => Assert.StartsWith(
                "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Original_named(\"",
                test.TestId,
                StringComparison.Ordinal));
        Assert.All(
            selected,
            test => Assert.Contains(
                "TestNameCaseFixture.Named_",
                test.FullName,
                StringComparison.Ordinal));
    }

    static TestingDiscoveredTest CollectStub()
    {
        var discoverer = new NUnitHostTestDiscoverer();
        return discoverer.Discover(FixturePath)
            .Single(test => test.MethodName == "Stub_leaf"
                && test.TestId.Contains("CollapsedSourceStubFixture", StringComparison.Ordinal));
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

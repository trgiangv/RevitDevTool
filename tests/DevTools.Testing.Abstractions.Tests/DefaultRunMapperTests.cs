using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Tests;

[TestClass]
public sealed class DefaultRunMapperTests
{
    private static readonly IReadOnlyList<TestDiscoveredTest> Discovered =
    [
        new TestDiscoveredTest("uid-1", "One"),
        new TestDiscoveredTest("uid-2", "Two"),
        new TestDiscoveredTest("uid-1", "One again"),
    ];

    private readonly ITestRunMapper _mapper = DefaultRunMapper.Instance;

    [TestMethod]
    public void All_and_framework_filter_pass_through()
    {
        Assert.AreEqual(TestSelectionKind.All, _mapper.ToRunSelection(TestSelection.All, Discovered).Kind);

        var filter = TestSelection.FromFrameworkFilter("x", "y");
        Assert.AreSame(filter, _mapper.ToRunSelection(filter, Discovered));
    }

    [TestMethod]
    public void Empty_test_ids_stay_empty_and_run_nothing()
    {
        var mapped = _mapper.ToRunSelection(TestSelection.FromTestIds([]), Discovered);

        Assert.AreEqual(TestSelectionKind.TestIds, mapped.Kind);
        Assert.AreEqual(0, mapped.TestIds.Count);
    }

    [TestMethod]
    public void Names_collapse_to_distinct_discovered_ids()
    {
        var mapped = _mapper.ToRunSelection(TestSelection.FromNames(["One"]), Discovered);

        Assert.AreEqual(TestSelectionKind.TestIds, mapped.Kind);
        Assert.AreSequenceEqual(new[] { "uid-1", "uid-2" }, mapped.TestIds);
    }

    [TestMethod]
    public void Test_ids_run_exactly_what_the_discoverer_returned()
    {
        // A discoverer may return more than was asked for (MSTest adds [DependsOn] prerequisites).
        var discovered = new[]
        {
            new TestDiscoveredTest("uid-2", "Two"),
            new TestDiscoveredTest("uid-1", "One"),
        };

        var mapped = _mapper.ToRunSelection(TestSelection.FromTestIds(["uid-2"]), discovered);

        Assert.AreSequenceEqual(new[] { "uid-2", "uid-1" }, mapped.TestIds);
    }

    [TestMethod]
    public void Host_results_are_not_folded()
    {
        var host = new[] { new TestCaseResult("uid-1", "One", TestOutcomes.Passed, 0, null, null, null, null, [], []) };

        Assert.AreSame(host, _mapper.FoldResults(TestSelection.All, Discovered, host));
    }

    [TestMethod]
    public void Unreported_cases_are_reported_as_errors()
    {
        var host = new[] { new TestCaseResult("uid-1", "One", TestOutcomes.Passed, 0, null, null, null, null, [], []) };

        var missing = _mapper.ResultsForUnreported(TestSelection.All, Discovered, host).Single();

        Assert.AreEqual("uid-2", missing.TestId);
        Assert.AreEqual(TestOutcomes.Error, missing.Outcome);
        Assert.Contains("did not report a result", missing.Message!, StringComparison.Ordinal);
    }
}
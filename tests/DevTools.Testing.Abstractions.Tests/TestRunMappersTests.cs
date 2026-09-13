using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Tests;

[TestClass]
public sealed class TestRunMappersTests
{
    static readonly TestSelection Requested = TestSelection.FromTestIds(["case-1", "case-2"]);
    static readonly IReadOnlyList<TestDiscoveredTest> Discovered =
    [
        new TestDiscoveredTest("case-1", "One"),
        new TestDiscoveredTest("case-2", "Two"),
    ];
    static readonly IReadOnlyList<TestCaseResult> HostResults =
    [
        CreateResult("case-1", "Passed"),
        CreateResult("case-2", "Failed"),
    ];

    [TestMethod]
    public void PassThrough_returns_requested_selection_unchanged()
    {
        var mapped = PassThroughRunMapper.Instance.ToRunSelection(Requested, Discovered);
        Assert.AreSame(Requested, mapped);
        Assert.AreSequenceEqual(Requested.TestIds, mapped.TestIds);
    }

    [TestMethod]
    public void PassThrough_returns_host_results_unchanged()
    {
        var folded = PassThroughRunMapper.Instance.FoldResults(Requested, Discovered, HostResults);
        Assert.AreSame(HostResults, folded);
    }

    [TestMethod]
    public void PassThrough_reports_no_unreported_cases()
    {
        var unreported = PassThroughRunMapper.Instance.ResultsForUnreported(Requested, Discovered, HostResults);
        Assert.IsEmpty(unreported);
    }

    static TestCaseResult CreateResult(string testId, string outcome) =>
        new(
            testId,
            testId,
            outcome,
            1,
            null,
            null,
            null,
            null,
            [],
            []);
}

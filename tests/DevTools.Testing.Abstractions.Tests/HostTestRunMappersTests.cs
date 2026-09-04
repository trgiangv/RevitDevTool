using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Tests;

public sealed class HostTestRunMappersTests
{
    static readonly TestingSelection Requested = new(["case-1", "case-2"]);
    static readonly IReadOnlyList<TestingDiscoveredTest> Discovered =
    [
        new TestingDiscoveredTest("case-1", "One"),
        new TestingDiscoveredTest("case-2", "Two"),
    ];
    static readonly IReadOnlyList<TestingCaseResult> HostResults =
    [
        CreateResult("case-1", "Passed"),
        CreateResult("case-2", "Failed"),
    ];

    [Fact]
    public void PassThrough_returns_requested_selection_unchanged()
    {
        var mapped = HostTestRunMappers.PassThrough.ToHostSelection(Requested, Discovered);
        Assert.Same(Requested, mapped);
        Assert.Equal(Requested.TestIds, mapped.TestIds);
    }

    [Fact]
    public void PassThrough_returns_host_results_unchanged()
    {
        var folded = HostTestRunMappers.PassThrough.FoldResults(Requested, Discovered, HostResults);
        Assert.Same(HostResults, folded);
    }

    [Fact]
    public void PassThrough_reports_no_unreported_cases()
    {
        var unreported = HostTestRunMappers.PassThrough.ResultsForUnreported(Requested, Discovered, HostResults);
        Assert.Empty(unreported);
    }

    static TestingCaseResult CreateResult(string testId, string outcome) =>
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

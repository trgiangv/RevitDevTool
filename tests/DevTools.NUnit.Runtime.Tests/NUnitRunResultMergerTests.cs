using DevTools.NUnit.Runtime;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.Runtime.Tests;

public sealed class NUnitRunResultMergerTests
{
    static TestingCaseResult Case(string testId) =>
        new(testId, testId, TestingOutcomes.Passed, 1, null, null, null, null, [], []);

    [Fact]
    public void Merge_returns_framework_cases_when_no_aborted_cases_exist()
    {
        var framework = new[] { Case("a"), Case("b") };

        var merged = NUnitRunResultMerger.Merge(framework, []);

        Assert.Same(framework, merged);
    }

    [Fact]
    public void Merge_returns_aborted_cases_when_framework_produced_none()
    {
        var aborted = new[] { Case("a") };

        var merged = NUnitRunResultMerger.Merge([], aborted);

        Assert.Same(aborted, merged);
    }

    [Fact]
    public void Merge_appends_aborted_cases_without_duplicating_test_ids()
    {
        var framework = new[] { Case("a"), Case("b") };
        var aborted = new[] { Case("b"), Case("c") };

        var merged = NUnitRunResultMerger.Merge(framework, aborted);

        Assert.Equal(["a", "b", "c"], merged.Select(result => result.TestId));
    }
}

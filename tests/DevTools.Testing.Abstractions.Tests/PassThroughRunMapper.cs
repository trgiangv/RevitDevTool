using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Tests;

internal static class PassThroughRunMapper
{
    public static IHostTestRunMapper Instance { get; } = new Mapper();

    private sealed class Mapper : IHostTestRunMapper
    {
        public TestingSelection ToHostSelection(
            TestingSelection requested,
            IReadOnlyList<TestingDiscoveredTest> discovered) =>
            requested;

        public IReadOnlyList<TestingCaseResult> FoldResults(
            TestingSelection requested,
            IReadOnlyList<TestingDiscoveredTest> discovered,
            IReadOnlyList<TestingCaseResult> hostResults) =>
            hostResults;

        public IReadOnlyList<TestingCaseResult> ResultsForUnreported(
            TestingSelection requested,
            IReadOnlyList<TestingDiscoveredTest> discovered,
            IReadOnlyList<TestingCaseResult> hostResults) =>
            [];
    }
}

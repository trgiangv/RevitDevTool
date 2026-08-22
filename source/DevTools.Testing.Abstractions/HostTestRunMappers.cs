using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions;

public static class HostTestRunMappers
{
    public static IHostTestRunMapper PassThrough { get; } = new PassThroughMapper();

    private sealed class PassThroughMapper : IHostTestRunMapper
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

using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Tests;

internal static class PassThroughRunMapper
{
    public static ITestRunMapper Instance { get; } = new Mapper();

    private sealed class Mapper : ITestRunMapper
    {
        public TestSelection ToRunSelection(
            TestSelection requested,
            IReadOnlyList<TestDiscoveredTest> discovered) =>
            requested;

        public IReadOnlyList<TestCaseResult> FoldResults(
            TestSelection requested,
            IReadOnlyList<TestDiscoveredTest> discovered,
            IReadOnlyList<TestCaseResult> hostResults) =>
            hostResults;

        public IReadOnlyList<TestCaseResult> ResultsForUnreported(
            TestSelection requested,
            IReadOnlyList<TestDiscoveredTest> discovered,
            IReadOnlyList<TestCaseResult> hostResults) =>
            [];
    }
}

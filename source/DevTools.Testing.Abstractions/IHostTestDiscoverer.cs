using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions;

/// <summary>
/// Testhost-owned local discovery and identity. The adapter control plane
/// publishes MTP TestNodes; it does not invent or parse framework names.
/// The testhost plug-in registers an implementation at process start.
/// </summary>
public interface IHostTestDiscoverer
{
    IReadOnlyList<TestingDiscoveredTest> Discover(string assemblyPath);

    IReadOnlyList<TestingDiscoveredTest> Select(
        string assemblyPath,
        TestingSelection selection);

    TestingSelection ToHostSelection(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered);

    IReadOnlyList<TestingCaseResult> FoldResults(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered,
        IReadOnlyList<TestingCaseResult> hostResults);

    IReadOnlyList<TestingCaseResult> ResultsForUnreported(
        TestingSelection requested,
        IReadOnlyList<TestingDiscoveredTest> discovered,
        IReadOnlyList<TestingCaseResult> hostResults);
}

public static class HostTestDiscovery
{
    public static IHostTestDiscoverer? Provider { get; set; }
}

public sealed class HostTestDiscoveryFailedException : Exception
{
    public HostTestDiscoveryFailedException(string message)
        : base(message)
    {
    }
}

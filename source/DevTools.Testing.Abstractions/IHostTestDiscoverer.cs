using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions;

/// <summary>
/// Testhost-owned local discovery. The adapter publishes MTP TestNodes from
/// these results; it does not invent framework-specific test identities.
/// </summary>
public interface IHostTestDiscoverer
{
    IReadOnlyList<TestingDiscoveredTest> Discover(string assemblyPath);

    IReadOnlyList<TestingDiscoveredTest> Discover(
        string assemblyPath,
        TestingDiscoveryOptions options);

    IReadOnlyList<TestingDiscoveredTest> Select(
        string assemblyPath,
        TestingSelection selection);

    IReadOnlyList<TestingDiscoveredTest> Select(
        string assemblyPath,
        TestingSelection selection,
        TestingDiscoveryOptions options);
}

public static class HostTestDiscovery
{
    public static IHostTestDiscoverer? Provider { get; set; }

    public static IHostTestRunMapper? RunMapper { get; set; }
}

public sealed class HostTestDiscoveryFailedException(string message) : Exception(message);

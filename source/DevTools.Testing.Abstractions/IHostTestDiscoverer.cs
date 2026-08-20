using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions;

/// <summary>
/// Testhost-owned local discovery. The adapter control plane does not invent
/// test identities; a provider supplies opaque ids that the in-host engine
/// understands. The testhost plug-in registers an implementation at process start.
/// </summary>
public interface IHostTestDiscoverer
{
    IReadOnlyList<TestingDiscoveredTest> Discover(string assemblyPath);

    IReadOnlyList<TestingDiscoveredTest> Select(
        string assemblyPath,
        TestingSelection selection);
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

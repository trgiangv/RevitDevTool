using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.TestAdapter;

/// <summary>
/// Framework-owned local discovery. The adapter control plane does not invent
/// test identities; a provider supplies opaque ids that the in-host engine
/// understands. NUnit registers this from <c>DevTools.NUnit.MTP</c>.
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

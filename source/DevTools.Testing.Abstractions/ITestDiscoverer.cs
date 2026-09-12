using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions;

/// <summary>
/// Local discovery. The adapter publishes MTP TestNodes from these results;
/// it does not invent framework-specific test identities.
/// </summary>
public interface ITestDiscoverer
{
    IReadOnlyList<TestDiscoveredTest> Discover(string assemblyPath, TestSelection selection);
}

public sealed record TestingDiscoveryBridge(
    ITestDiscoverer Discoverer,
    ITestRunMapper RunMapper);

/// <summary>
/// Testhost local-discovery handoff. Named <c>TestingDiscovery</c> so TUnit
/// consumer testhosts that compile the sibling hook do not collide with
/// <c>HookType.TestDiscovery</c> (CS0229).
/// </summary>
public static class TestingDiscovery
{
    private static readonly Lock Gate = new();
    private static TestingDiscoveryBridge? _current;

    public static TestingDiscoveryBridge? Current
    {
        get
        {
            lock (Gate)
                return _current;
        }
    }

    public static ITestDiscoverer? Provider => Current?.Discoverer;

    public static ITestRunMapper? RunMapper => Current?.RunMapper;

    public static void Register(ITestDiscoverer discoverer, ITestRunMapper runMapper)
    {
        ArgumentNullException.ThrowIfNull(discoverer);
        ArgumentNullException.ThrowIfNull(runMapper);
        var bridge = new TestingDiscoveryBridge(discoverer, runMapper);
        lock (Gate)
        {
            if (_current is not null
                && (!ReferenceEquals(_current.Discoverer, discoverer)
                    || !ReferenceEquals(_current.RunMapper, runMapper)))
            {
                throw new InvalidOperationException(
                    "TestingDiscovery is already registered with a different discoverer or run mapper.");
            }

            _current = bridge;
        }
    }

    internal static void Clear()
    {
        lock (Gate)
            _current = null;
    }
}

public sealed class TestingDiscoveryFailedException(string message) : Exception(message);

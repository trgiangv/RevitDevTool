using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions;

/// <summary>
/// Testhost-owned local discovery. The adapter publishes MTP TestNodes from
/// these results; it does not invent framework-specific test identities.
/// </summary>
public interface IHostTestDiscoverer
{
    IReadOnlyList<TestingDiscoveredTest> Discover(string assemblyPath, TestingSelection selection);
}

public sealed record HostTestFrameworkBridge(
    IHostTestDiscoverer Discoverer,
    IHostTestRunMapper RunMapper);

public static class HostTestDiscovery
{
    private static readonly object Gate = new();
    private static HostTestFrameworkBridge? _current;

    public static HostTestFrameworkBridge? Current
    {
        get
        {
            lock (Gate)
                return _current;
        }
    }

    public static IHostTestDiscoverer? Provider => Current?.Discoverer;

    public static IHostTestRunMapper? RunMapper => Current?.RunMapper;

    public static void Register(IHostTestDiscoverer discoverer, IHostTestRunMapper runMapper)
    {
        ArgumentNullException.ThrowIfNull(discoverer);
        ArgumentNullException.ThrowIfNull(runMapper);
        var bridge = new HostTestFrameworkBridge(discoverer, runMapper);
        lock (Gate)
        {
            if (_current is not null
                && (!ReferenceEquals(_current.Discoverer, discoverer)
                    || !ReferenceEquals(_current.RunMapper, runMapper)))
            {
                throw new InvalidOperationException(
                    "HostTestDiscovery is already registered with a different discoverer or run mapper.");
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

public sealed class HostTestDiscoveryFailedException(string message) : Exception(message);

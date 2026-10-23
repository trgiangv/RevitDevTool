using System.Reflection;

namespace DevTools.AssemblyIsolation;

/// <summary>
/// Host API bindings for an isolation plan. Subclasses only supply
/// compile-time type anchors and optional already-loaded simple names.
/// The set is captured on first use and reused for the process lifetime.
/// </summary>
public abstract class HostAssemblies
{
    private readonly Lazy<Assembly[]> _cached;

    protected HostAssemblies()
    {
        _cached = new Lazy<Assembly[]>(Capture, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    protected abstract IEnumerable<Assembly> LoadedByType { get; }

    protected abstract IReadOnlyList<string> LoadedByName { get; }

    public IReadOnlyList<Assembly> All() => _cached.Value;

    private Assembly[] Capture() => AssemblyHelper.CaptureHostAssemblies(LoadedByType, LoadedByName);
}
